using Library.Application.Abstractions;
using Library.Application.Common;
using Library.Application.Contracts;
using Library.Domain;
using Library.Domain.Entities;

namespace Library.Application.Catalog;

/// <summary>
/// Everything a librarian does to the catalogue itself: adding titles, correcting
/// them, registering extra copies, retiring records.
/// </summary>
public class CatalogService(
    IBookRepository books,
    ICatalogQueries queries,
    IUnitOfWork unitOfWork,
    IBarcodeGenerator barcodes)
{
    public Task<PagedResult<BookSummary>> SearchAsync(SearchBooksQuery query, CancellationToken ct = default) =>
        queries.SearchAsync(query, ct);

    public Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        queries.GetCategoriesAsync(ct);

    public async Task<Result<BookDetail>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await queries.GetDetailAsync(id, ct);
        return detail is null
            ? Result<BookDetail>.NotFound($"No book with id {id}.")
            : Result<BookDetail>.Success(detail);
    }

    public async Task<Result<BookDetail>> CreateAsync(CreateBookRequest request, CancellationToken ct = default)
    {
        if (request.InitialCopies is < 0 or > 50)
            return Result<BookDetail>.Invalid("A title can be registered with between 0 and 50 copies at a time.");

        Book book;
        try
        {
            book = new Book(request.Title, request.Author, request.Isbn);
            book.Describe(request.Synopsis, request.Category, request.PublishedYear, request.Tags);
        }
        catch (DomainException ex)
        {
            return Result<BookDetail>.Invalid(ex.Message);
        }

        // Checked before insert so the caller gets a clear conflict rather than a
        // unique-index violation. The index remains the real guarantee.
        if (book.Isbn is not null && await books.IsbnExistsAsync(book.Isbn, null, ct))
            return Result<BookDetail>.Conflict($"A book with ISBN {book.Isbn} is already in the catalogue.");

        try
        {
            for (var i = 0; i < request.InitialCopies; i++)
                book.AddCopy(await barcodes.NextAsync(ct));
        }
        catch (DomainException ex)
        {
            return Result<BookDetail>.Invalid(ex.Message);
        }

        books.Add(book);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetAsync(book.Id, ct);
    }

    public async Task<Result<BookDetail>> UpdateAsync(
        Guid id,
        UpdateBookRequest request,
        CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(id, includeCopies: false, ct);
        if (book is null)
            return Result<BookDetail>.NotFound($"No book with id {id}.");

        try
        {
            book.Rename(request.Title, request.Author);
            book.SetIsbn(request.Isbn);
            book.Describe(request.Synopsis, request.Category, request.PublishedYear, request.Tags);
        }
        catch (DomainException ex)
        {
            return Result<BookDetail>.Invalid(ex.Message);
        }

        if (book.Isbn is not null && await books.IsbnExistsAsync(book.Isbn, id, ct))
            return Result<BookDetail>.Conflict($"Another book already carries ISBN {book.Isbn}.");

        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<BookDetail>> AddCopyAsync(
        Guid id,
        AddCopyRequest request,
        CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(id, includeCopies: true, ct);
        if (book is null)
            return Result<BookDetail>.NotFound($"No book with id {id}.");

        var barcode = string.IsNullOrWhiteSpace(request.Barcode)
            ? await barcodes.NextAsync(ct)
            : request.Barcode.Trim();

        if (await books.BarcodeExistsAsync(barcode, ct))
            return Result<BookDetail>.Conflict($"Barcode {barcode} is already in use.");

        try
        {
            book.AddCopy(barcode);
        }
        catch (DomainException ex)
        {
            return Result<BookDetail>.Invalid(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>
    /// Removes a title and its copies. Refused while any copy is out: deleting it
    /// would erase the record of a book a member is still holding.
    /// </summary>
    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var book = await books.GetByIdAsync(id, includeCopies: true, ct);
        if (book is null)
            return Result<bool>.NotFound($"No book with id {id}.");

        try
        {
            book.EnsureCanBeRemoved();
        }
        catch (DomainException ex)
        {
            return Result<bool>.Conflict(ex.Message);
        }

        books.Remove(book);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
