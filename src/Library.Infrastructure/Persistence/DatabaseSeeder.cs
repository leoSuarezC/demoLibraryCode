using Library.Domain;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence;

/// <summary>
/// Populates an empty database with a catalogue worth demonstrating.
/// </summary>
/// <remarks>
/// Seeding is deliberately not "one book, one copy". The interesting behaviour of a
/// library only shows up with titles held in several copies, some out on loan and at
/// least one overdue — otherwise availability counts, the borrowing limit and the
/// overdue dashboard all render empty and look unimplemented.
/// </remarks>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(LibraryDbContext db, CancellationToken ct = default)
    {
        if (await db.Books.AnyAsync(ct)) return;

        var members = CreateMembers();
        db.Members.AddRange(members);

        var books = CreateBooks();
        db.Books.AddRange(books);

        await db.SaveChangesAsync(ct);

        db.Loans.AddRange(CreateOpeningLoans(books, members));
        await db.SaveChangesAsync(ct);
    }

    private static Member[] CreateMembers() =>
    [
        new("Ada Lovelace",      "ada.lovelace@example.com",   "MEM-0001"),
        new("Grace Hopper",      "grace.hopper@example.com",   "MEM-0002"),
        new("Alan Turing",       "alan.turing@example.com",    "MEM-0003"),
        new("Katherine Johnson", "k.johnson@example.com",      "MEM-0004"),
        new("Margaret Hamilton", "m.hamilton@example.com",     "MEM-0005"),
        new("Barbara Liskov",    "barbara.liskov@example.com", "MEM-0006")
    ];

    private static List<Book> CreateBooks()
    {
        var catalogue = new (string Title, string Author, string Isbn12, string Category, int Year, string Tags, int Copies)[]
        {
            ("Dune", "Frank Herbert", "978044101359", "Science Fiction", 1965, "space opera,politics,ecology", 3),
            ("The Left Hand of Darkness", "Ursula K. Le Guin", "978044100731", "Science Fiction", 1969, "anthropology,gender,classic", 2),
            ("The Dispossessed", "Ursula K. Le Guin", "978006051275", "Science Fiction", 1974, "utopia,physics,anarchism", 2),
            ("Neuromancer", "William Gibson", "978044156956", "Science Fiction", 1984, "cyberpunk,ai,noir", 2),
            ("Snow Crash", "Neal Stephenson", "978055338095", "Science Fiction", 1992, "cyberpunk,linguistics,satire", 1),
            ("Foundation", "Isaac Asimov", "978055329335", "Science Fiction", 1951, "empire,psychohistory,classic", 3),
            ("Kindred", "Octavia E. Butler", "978080708369", "Fiction", 1979, "time travel,history,slavery", 2),
            ("Parable of the Sower", "Octavia E. Butler", "978044667550", "Science Fiction", 1993, "dystopia,climate,resilience", 2),
            ("The Three-Body Problem", "Cixin Liu", "978076538547", "Science Fiction", 2008, "first contact,physics,translated", 2),
            ("Piranesi", "Susanna Clarke", "978163557563", "Fantasy", 2020, "labyrinth,mystery,literary", 1),
            ("The Name of the Wind", "Patrick Rothfuss", "978075640407", "Fantasy", 2007, "magic,music,coming of age", 2),
            ("Clean Code", "Robert C. Martin", "978013235088", "Software", 2008, "craftsmanship,refactoring,practices", 3),
            ("Refactoring", "Martin Fowler", "978013475759", "Software", 2018, "refactoring,design,patterns", 2),
            ("Domain-Driven Design", "Eric Evans", "978032112521", "Software", 2003, "ddd,modelling,architecture", 2),
            ("Designing Data-Intensive Applications", "Martin Kleppmann", "978144937332", "Software", 2017, "distributed systems,databases,scale", 3),
            ("The Pragmatic Programmer", "Andrew Hunt", "978020161622", "Software", 1999, "craftsmanship,career,practices", 2),
            ("Thinking, Fast and Slow", "Daniel Kahneman", "978037453355", "Non-fiction", 2011, "psychology,decisions,bias", 2),
            ("Sapiens", "Yuval Noah Harari", "978006231609", "Non-fiction", 2011, "history,anthropology,society", 2)
        };

        var books = new List<Book>(catalogue.Length);
        var sequence = 1;

        foreach (var entry in catalogue)
        {
            var book = new Book(entry.Title, entry.Author, CompleteIsbn13(entry.Isbn12));
            book.Describe(
                synopsis: null,
                category: entry.Category,
                publishedYear: entry.Year,
                tags: entry.Tags.Split(','));

            for (var i = 0; i < entry.Copies; i++)
                book.AddCopy($"LIB-{sequence++:D5}");

            books.Add(book);
        }

        return books;
    }

    /// <summary>
    /// Opens a handful of loans so the dashboard has something to show, including one
    /// that is already past its due date.
    /// </summary>
    private static List<Loan> CreateOpeningLoans(List<Book> books, Member[] members)
    {
        var now = DateTime.UtcNow;
        var loans = new List<Loan>();

        // (book index, member index, days ago the loan started)
        var openingLoans = new[]
        {
            (Book: 0,  Member: 0, DaysAgo: 5),
            (Book: 0,  Member: 1, DaysAgo: 12),
            (Book: 3,  Member: 2, DaysAgo: 30), // overdue: 21-day term expired 9 days ago
            (Book: 11, Member: 3, DaysAgo: 2),
            (Book: 14, Member: 4, DaysAgo: 26), // overdue by 5 days
            (Book: 8,  Member: 5, DaysAgo: 1)
        };

        foreach (var (bookIndex, memberIndex, daysAgo) in openingLoans)
        {
            var copy = books[bookIndex].Copies.FirstOrDefault(c => c.IsAvailable);
            if (copy is null) continue;

            copy.MarkOnLoan();

            loans.Add(new Loan(
                copy.Id,
                members[memberIndex].Id,
                idempotencyKey: $"seed-{copy.Id}",
                checkedOutAtUtc: now.AddDays(-daysAgo),
                loanPeriod: LoanPolicy.LoanPeriod));
        }

        return loans;
    }

    /// <summary>
    /// Appends the ISBN-13 check digit to a 12-digit prefix.
    /// </summary>
    /// <remarks>
    /// The domain validates check digits on entry, so seed data has to be arithmetically
    /// correct rather than merely 13 digits long. Computing it here keeps the fixture
    /// honest instead of disabling validation for seeding.
    /// </remarks>
    private static string CompleteIsbn13(string first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);

        var check = (10 - sum % 10) % 10;
        return first12 + check;
    }
}
