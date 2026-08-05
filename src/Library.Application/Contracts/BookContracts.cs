namespace Library.Application.Contracts;

/// <summary>Row shape for catalogue listings and search results.</summary>
public record BookSummary(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Category,
    int? PublishedYear,
    int TotalCopies,
    int AvailableCopies)
{
    public bool IsAvailable => AvailableCopies > 0;
}

/// <summary>Everything shown on a single book's page, including its physical items.</summary>
public record BookDetail(
    Guid Id,
    string Title,
    string Author,
    string? Isbn,
    string? Synopsis,
    string? Category,
    int? PublishedYear,
    IReadOnlyList<string> Tags,
    IReadOnlyList<CopyDetail> Copies,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public int TotalCopies => Copies.Count;
    public int AvailableCopies => Copies.Count(c => c.Status == "Available");
}

/// <summary>A physical item, with the open loan attached when it is out.</summary>
public record CopyDetail(
    Guid Id,
    string Barcode,
    string Status,
    DateTime AcquiredAtUtc,
    LoanSummary? CurrentLoan);

/// <param name="InitialCopies">
/// How many physical items to register alongside the title. Barcodes are generated
/// from the ISBN when present, otherwise from the title.
/// </param>
public record CreateBookRequest(
    string Title,
    string Author,
    string? Isbn = null,
    string? Synopsis = null,
    string? Category = null,
    int? PublishedYear = null,
    IReadOnlyList<string>? Tags = null,
    int InitialCopies = 1);

public record UpdateBookRequest(
    string Title,
    string Author,
    string? Isbn = null,
    string? Synopsis = null,
    string? Category = null,
    int? PublishedYear = null,
    IReadOnlyList<string>? Tags = null);

public record AddCopyRequest(string? Barcode = null);
