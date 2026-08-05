using Library.Application.Common;
using Library.Application.Contracts;

namespace Library.Application.Abstractions;

/// <summary>
/// Read-side access to the catalogue, kept separate from <see cref="IBookRepository"/>.
/// </summary>
/// <remarks>
/// Listing and searching are shaped by what the screen needs, not by the aggregate:
/// a results grid wants "available copies" as a number, which is an aggregate over a
/// child table. Serving that through the write model means loading every copy of
/// every match. These queries project straight into DTOs instead, and the search one
/// is backed by a stored procedure.
/// </remarks>
public interface ICatalogQueries
{
    Task<PagedResult<BookSummary>> SearchAsync(SearchBooksQuery query, CancellationToken ct = default);

    Task<BookDetail?> GetDetailAsync(Guid bookId, CancellationToken ct = default);

    /// <summary>Distinct categories present in the catalogue, for filter dropdowns.</summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}
