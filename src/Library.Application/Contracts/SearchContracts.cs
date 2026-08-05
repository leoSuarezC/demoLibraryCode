namespace Library.Application.Contracts;

/// <summary>
/// Search criteria for the catalogue.
/// </summary>
/// <param name="Query">Free text matched against title, author, ISBN, category and tags.</param>
/// <param name="Category">Exact category filter, applied on top of <paramref name="Query"/>.</param>
/// <param name="AvailableOnly">Restrict to titles with at least one copy on the shelf.</param>
public record SearchBooksQuery(
    string? Query = null,
    string? Category = null,
    bool AvailableOnly = false,
    int Page = 1,
    int PageSize = 20)
{
    public const int MaxPageSize = 100;

    /// <summary>
    /// Clamps caller-supplied paging into a sane range. A search endpoint is a public
    /// surface; <c>?pageSize=100000</c> should not become a table scan on request.
    /// </summary>
    public SearchBooksQuery Normalised() => this with
    {
        Query = string.IsNullOrWhiteSpace(Query) ? null : Query.Trim(),
        Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim(),
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize is < 1 or > MaxPageSize ? 20 : PageSize
    };
}
