using Library.Domain.Entities;

namespace Library.Application.Abstractions;

/// <summary>
/// Write-side access to the book aggregate: load it, change it, save it.
/// </summary>
/// <remarks>
/// Reads for listing and search deliberately do not come through here — see
/// <see cref="ICatalogQueries"/>. Loading full aggregates to render a grid is
/// wasteful, and the search path needs T-SQL this interface has no business exposing.
/// </remarks>
public interface IBookRepository
{
    /// <param name="includeCopies">Load the physical items too. Needed for anything that lends or deletes.</param>
    Task<Book?> GetByIdAsync(Guid id, bool includeCopies = true, CancellationToken ct = default);

    Task<bool> IsbnExistsAsync(string isbn, Guid? excludingBookId = null, CancellationToken ct = default);

    Task<bool> BarcodeExistsAsync(string barcode, CancellationToken ct = default);

    void Add(Book book);

    void Remove(Book book);
}
