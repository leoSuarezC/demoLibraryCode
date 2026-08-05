namespace Library.Application.Abstractions;

/// <summary>What a language model proposes for a title it was given.</summary>
public record AiMetadataSuggestion(
    string? Synopsis,
    string? Category,
    int? PublishedYear,
    IReadOnlyList<string> Tags);

/// <summary>
/// Fills in descriptive metadata a librarian would otherwise type by hand.
/// </summary>
/// <remarks>
/// The suggestion is returned to the client to populate the form, never written to
/// the catalogue directly: a model can be confidently wrong about a publication year,
/// and a human confirming the result is what keeps the catalogue trustworthy.
/// </remarks>
public interface IAiMetadataService
{
    /// <summary>True when a provider is configured; false makes the UI hide the feature rather than offer a button that always fails.</summary>
    bool IsConfigured { get; }

    Task<AiMetadataSuggestion?> SuggestAsync(string title, string author, CancellationToken ct = default);
}
