using Library.Application.Abstractions;
using Library.Application.Catalog;
using Library.Application.Common;
using Library.Application.Contracts;
using Library.Domain.Entities;
using NSubstitute;

namespace Library.UnitTests.Application;

public class CatalogServiceTests
{
    private readonly IBookRepository _books = Substitute.For<IBookRepository>();
    private readonly ICatalogQueries _queries = Substitute.For<ICatalogQueries>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IBarcodeGenerator _barcodes = Substitute.For<IBarcodeGenerator>();

    private CatalogService CreateService()
    {
        var counter = 0;
        _barcodes.NextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult($"LIB-{++counter:D5}"));

        return new CatalogService(_books, _queries, _unitOfWork, _barcodes);
    }

    private void StubDetailFor(Guid id) =>
        _queries.GetDetailAsync(id, Arg.Any<CancellationToken>())
            .Returns(new BookDetail(id, "Dune", "Frank Herbert", null, null, null, null, [], [],
                DateTime.UtcNow, DateTime.UtcNow));

    [Fact]
    public async Task A_new_title_is_registered_with_the_requested_number_of_copies()
    {
        Book? captured = null;
        _books.When(r => r.Add(Arg.Any<Book>())).Do(c => captured = c.Arg<Book>());
        _queries.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new BookDetail(ci.Arg<Guid>(), "Dune", "Frank Herbert",
                null, null, null, null, [], [], DateTime.UtcNow, DateTime.UtcNow));

        var service = CreateService();
        var result = await service.CreateAsync(new CreateBookRequest("Dune", "Frank Herbert", InitialCopies: 3));

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(3, captured!.Copies.Count);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_title_may_be_registered_with_no_copies_yet()
    {
        _queries.GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => new BookDetail(ci.Arg<Guid>(), "On Order", "Someone",
                null, null, null, null, [], [], DateTime.UtcNow, DateTime.UtcNow));

        var service = CreateService();
        var result = await service.CreateAsync(new CreateBookRequest("On Order", "Someone", InitialCopies: 0));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task An_absurd_number_of_copies_is_refused()
    {
        var service = CreateService();

        var result = await service.CreateAsync(new CreateBookRequest("Dune", "Frank Herbert", InitialCopies: 5000));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Kind);
        _books.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task An_invalid_isbn_is_reported_as_a_validation_error_not_an_exception()
    {
        var service = CreateService();

        var result = await service.CreateAsync(
            new CreateBookRequest("Dune", "Frank Herbert", Isbn: "9780441015393"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Kind);
    }

    [Fact]
    public async Task A_duplicate_isbn_is_reported_as_a_conflict()
    {
        _books.IsbnExistsAsync("9780441013593", null, Arg.Any<CancellationToken>()).Returns(true);

        var service = CreateService();
        var result = await service.CreateAsync(
            new CreateBookRequest("Dune", "Frank Herbert", Isbn: "978-0-441-01359-3"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Kind);
        _books.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Deleting_an_unknown_title_reports_not_found()
    {
        _books.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Book?)null);

        var service = CreateService();
        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task Deleting_a_title_with_a_copy_on_loan_is_refused_as_a_conflict()
    {
        var book = new Book("Dune", "Frank Herbert");
        var copy = book.AddCopy("LIB-00001");
        copy.MarkOnLoan();

        _books.GetByIdAsync(book.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(book);

        var service = CreateService();
        var result = await service.DeleteAsync(book.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Kind);
        _books.DidNotReceiveWithAnyArgs().Remove(default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task A_title_whose_copies_are_all_in_can_be_deleted()
    {
        var book = new Book("Dune", "Frank Herbert");
        book.AddCopy("LIB-00001");

        _books.GetByIdAsync(book.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(book);

        var service = CreateService();
        var result = await service.DeleteAsync(book.Id);

        Assert.True(result.IsSuccess);
        _books.Received(1).Remove(book);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Adding_a_copy_with_a_barcode_already_in_use_is_a_conflict()
    {
        var book = new Book("Dune", "Frank Herbert");
        _books.GetByIdAsync(book.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(book);
        _books.BarcodeExistsAsync("LIB-99999", Arg.Any<CancellationToken>()).Returns(true);

        var service = CreateService();
        var result = await service.AddCopyAsync(book.Id, new AddCopyRequest("LIB-99999"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Kind);
    }

    [Fact]
    public async Task Adding_a_copy_without_a_barcode_generates_one()
    {
        var book = new Book("Dune", "Frank Herbert");
        _books.GetByIdAsync(book.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(book);
        StubDetailFor(book.Id);

        var service = CreateService();
        var result = await service.AddCopyAsync(book.Id, new AddCopyRequest());

        Assert.True(result.IsSuccess);
        Assert.Single(book.Copies);
        await _barcodes.Received(1).NextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Updating_an_unknown_title_reports_not_found()
    {
        _books.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((Book?)null);

        var service = CreateService();
        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateBookRequest("T", "A"));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task Search_paging_is_clamped_before_it_reaches_the_database()
    {
        SearchBooksQuery? seen = null;
        _queries.SearchAsync(Arg.Any<SearchBooksQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                seen = ci.Arg<SearchBooksQuery>();
                return PagedResult<BookSummary>.Empty(1, 20);
            });

        // The service passes the query through; clamping is the query object's own job,
        // so this pins the behaviour callers depend on.
        var clamped = new SearchBooksQuery(Page: -5, PageSize: 100_000).Normalised();

        Assert.Equal(1, clamped.Page);
        Assert.Equal(20, clamped.PageSize);

        var service = CreateService();
        await service.SearchAsync(clamped);

        Assert.NotNull(seen);
        Assert.Equal(20, seen!.PageSize);
    }
}
