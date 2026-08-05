using Library.Domain;
using Library.Domain.Entities;

namespace Library.UnitTests.Domain;

public class BookTests
{
    private static Book ABook() => new("Dune", "Frank Herbert", "9780441013593");

    [Fact]
    public void A_book_requires_a_title_and_an_author()
    {
        Assert.Throws<DomainException>(() => new Book("", "Frank Herbert"));
        Assert.Throws<DomainException>(() => new Book("Dune", "   "));
    }

    [Fact]
    public void An_invalid_isbn_is_rejected_on_entry() =>
        Assert.Throws<DomainException>(() => new Book("Dune", "Frank Herbert", "9780441015393"));

    [Fact]
    public void A_book_may_have_no_isbn_at_all()
    {
        var book = new Book("An Anonymous Manuscript", "Unknown", isbn: null);

        Assert.Null(book.Isbn);
    }

    [Fact]
    public void Copies_of_the_same_title_cannot_share_a_barcode()
    {
        var book = ABook();
        book.AddCopy("LIB-00001");

        var duplicate = Assert.Throws<DomainException>(() => book.AddCopy("lib-00001"));
        Assert.Contains("already registered", duplicate.Message);
    }

    [Fact]
    public void A_title_can_hold_several_copies()
    {
        var book = ABook();
        book.AddCopy("LIB-00001");
        book.AddCopy("LIB-00002");
        book.AddCopy("LIB-00003");

        Assert.Equal(3, book.Copies.Count);
        Assert.All(book.Copies, c => Assert.True(c.IsAvailable));
    }

    [Fact]
    public void A_title_cannot_be_removed_while_a_copy_is_on_loan()
    {
        var book = ABook();
        var copy = book.AddCopy("LIB-00001");
        book.AddCopy("LIB-00002");

        copy.MarkOnLoan();

        var refusal = Assert.Throws<DomainException>(book.EnsureCanBeRemoved);
        Assert.Contains("still on loan", refusal.Message);
    }

    [Fact]
    public void A_title_whose_copies_are_all_on_the_shelf_can_be_removed()
    {
        var book = ABook();
        book.AddCopy("LIB-00001");

        book.EnsureCanBeRemoved();   // must not throw
    }

    [Fact]
    public void An_implausible_publication_year_is_rejected()
    {
        var book = ABook();

        Assert.Throws<DomainException>(() => book.Describe(publishedYear: 1200));
        Assert.Throws<DomainException>(() => book.Describe(publishedYear: DateTime.UtcNow.Year + 5));
    }

    [Fact]
    public void Tags_are_trimmed_and_de_duplicated_case_insensitively()
    {
        var book = ABook();

        book.Describe(tags: [" sci-fi ", "SCI-FI", "classic", ""]);

        Assert.Equal("sci-fi,classic", book.Tags);
    }

    [Fact]
    public void Describe_leaves_untouched_fields_alone()
    {
        var book = ABook();
        book.Describe(synopsis: "A desert planet.", category: "Science Fiction", publishedYear: 1965);

        book.Describe(category: "Classics");

        Assert.Equal("A desert planet.", book.Synopsis);
        Assert.Equal("Classics", book.Category);
        Assert.Equal(1965, book.PublishedYear);
    }
}
