namespace Library.Domain.Entities;

/// <summary>
/// A bibliographic record: one row per <em>title</em>, not per physical item.
/// A library that owns three copies of "Dune" has one <see cref="Book"/> and
/// three <see cref="BookCopy"/> rows.
/// </summary>
/// <remarks>
/// Keeping titles and physical items apart is what allows the catalogue to answer
/// "how many are on the shelf right now?" instead of a single yes/no flag, and it
/// is the reason two members can borrow the same title at the same time.
/// </remarks>
public class Book
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public string Title { get; private set; } = string.Empty;
    public string Author { get; private set; } = string.Empty;

    /// <summary>ISBN-10 or ISBN-13, stored without separators. Optional: older stock often predates ISBN.</summary>
    public string? Isbn { get; private set; }

    public string? Synopsis { get; private set; }
    public string? Category { get; private set; }
    public int? PublishedYear { get; private set; }

    /// <summary>
    /// Comma-separated keywords. Deliberately denormalised: tags are only ever read
    /// as a whole alongside the book, never queried independently, so a child table
    /// would buy nothing but joins.
    /// </summary>
    public string? Tags { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private readonly List<BookCopy> _copies = [];
    public IReadOnlyCollection<BookCopy> Copies => _copies.AsReadOnly();

    // Required by EF Core materialisation.
    private Book() { }

    public Book(string title, string author, string? isbn = null)
    {
        Rename(title, author);
        SetIsbn(isbn);
    }

    public void Rename(string title, string author)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("A book must have a title.");
        if (string.IsNullOrWhiteSpace(author))
            throw new DomainException("A book must have an author.");

        Title = title.Trim();
        Author = author.Trim();
        Touch();
    }

    public void SetIsbn(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            Isbn = null;
            Touch();
            return;
        }

        var normalised = Isbn13.Normalise(isbn);
        if (!Isbn13.IsValid(normalised))
            throw new DomainException($"'{isbn}' is not a valid ISBN-10 or ISBN-13.");

        Isbn = normalised;
        Touch();
    }

    /// <summary>
    /// Applies descriptive metadata. Every argument is optional so the same method
    /// serves both a librarian editing one field and the AI enrichment flow filling
    /// several at once.
    /// </summary>
    public void Describe(
        string? synopsis = null,
        string? category = null,
        int? publishedYear = null,
        IEnumerable<string>? tags = null)
    {
        if (publishedYear is { } year && (year < 1450 || year > DateTime.UtcNow.Year + 1))
            throw new DomainException($"Publication year {year} is outside the plausible range.");

        Synopsis = string.IsNullOrWhiteSpace(synopsis) ? Synopsis : synopsis.Trim();
        Category = string.IsNullOrWhiteSpace(category) ? Category : category.Trim();
        PublishedYear = publishedYear ?? PublishedYear;

        if (tags is not null)
        {
            var cleaned = tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Tags = cleaned.Length == 0 ? null : string.Join(',', cleaned);
        }

        Touch();
    }

    /// <summary>Registers a new physical item on the shelf.</summary>
    public BookCopy AddCopy(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            throw new DomainException("A copy must carry a barcode.");

        var trimmed = barcode.Trim();
        if (_copies.Any(c => string.Equals(c.Barcode, trimmed, StringComparison.OrdinalIgnoreCase)))
            throw new DomainException($"Barcode '{trimmed}' is already registered for this title.");

        var copy = new BookCopy(Id, trimmed);
        _copies.Add(copy);
        Touch();
        return copy;
    }

    /// <summary>
    /// A title may only leave the catalogue once every physical item is accounted for.
    /// Deleting a title with an outstanding loan would orphan a book a member still holds.
    /// </summary>
    public void EnsureCanBeRemoved()
    {
        var onLoan = _copies.Count(c => c.Status == CopyStatus.OnLoan);
        if (onLoan > 0)
            throw new DomainException(
                $"Cannot remove '{Title}': {onLoan} cop{(onLoan == 1 ? "y is" : "ies are")} still on loan.");
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
