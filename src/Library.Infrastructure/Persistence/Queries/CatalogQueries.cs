using System.Data;
using Library.Application.Abstractions;
using Library.Application.Common;
using Library.Application.Contracts;
using Library.Application.Loans;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Queries;

/// <summary>
/// Read-side of the catalogue. Search runs through <c>usp_SearchBooks</c>; the
/// remaining reads are ordinary projections.
/// </summary>
public class CatalogQueries(LibraryDbContext db, IClock clock) : ICatalogQueries
{
    public async Task<PagedResult<BookSummary>> SearchAsync(SearchBooksQuery query, CancellationToken ct = default)
    {
        var q = query.Normalised();

        var connection = (SqlConnection)db.Database.GetDbConnection();
        var opened = false;
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            opened = true;
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "dbo.usp_SearchBooks";

            command.Parameters.Add(new SqlParameter("@Query", SqlDbType.NVarChar, 400)
            {
                Value = (object?)q.Query ?? DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@Category", SqlDbType.NVarChar, 100)
            {
                Value = (object?)q.Category ?? DBNull.Value
            });
            command.Parameters.Add(new SqlParameter("@AvailableOnly", SqlDbType.Bit) { Value = q.AvailableOnly });
            command.Parameters.Add(new SqlParameter("@Skip", SqlDbType.Int) { Value = (q.Page - 1) * q.PageSize });
            command.Parameters.Add(new SqlParameter("@Take", SqlDbType.Int) { Value = q.PageSize });

            var totalParam = new SqlParameter("@TotalCount", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(totalParam);

            var items = new List<BookSummary>(q.PageSize);

            await using (var reader = await command.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    items.Add(new BookSummary(
                        Id: reader.GetGuid(reader.GetOrdinal("Id")),
                        Title: reader.GetString(reader.GetOrdinal("Title")),
                        Author: reader.GetString(reader.GetOrdinal("Author")),
                        Isbn: reader.GetNullableString("Isbn"),
                        Category: reader.GetNullableString("Category"),
                        PublishedYear: reader.GetNullableInt("PublishedYear"),
                        TotalCopies: reader.GetInt32(reader.GetOrdinal("TotalCopies")),
                        AvailableCopies: reader.GetInt32(reader.GetOrdinal("AvailableCopies"))));
                }
            }

            // Output parameters are only populated once the reader is closed.
            var total = totalParam.Value is DBNull ? 0 : (int)totalParam.Value;

            return new PagedResult<BookSummary>(items, total, q.Page, q.PageSize);
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    public async Task<BookDetail?> GetDetailAsync(Guid bookId, CancellationToken ct = default)
    {
        var book = await db.Books
            .AsNoTracking()
            .Include(b => b.Copies)
            .FirstOrDefaultAsync(b => b.Id == bookId, ct);

        if (book is null) return null;

        var copyIds = book.Copies.Select(c => c.Id).ToArray();

        var openLoans = await db.Loans
            .AsNoTracking()
            .Include(l => l.Member)
            .Where(l => copyIds.Contains(l.BookCopyId) && l.ReturnedAtUtc == null)
            .ToDictionaryAsync(l => l.BookCopyId, ct);

        var now = clock.UtcNow;

        var copies = book.Copies
            .OrderBy(c => c.Barcode)
            .Select(c => new CopyDetail(
                c.Id,
                c.Barcode,
                c.Status.ToString(),
                c.AcquiredAtUtc,
                openLoans.TryGetValue(c.Id, out var loan)
                    ? loan.ToSummary(book.Title, c.Barcode, now)
                    : null))
            .ToList();

        return new BookDetail(
            book.Id,
            book.Title,
            book.Author,
            book.Isbn,
            book.Synopsis,
            book.Category,
            book.PublishedYear,
            SplitTags(book.Tags),
            copies,
            book.CreatedAtUtc,
            book.UpdatedAtUtc);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.Books
            .AsNoTracking()
            .Where(b => b.Category != null)
            .Select(b => b.Category!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    internal static IReadOnlyList<string> SplitTags(string? tags) =>
        string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

internal static class DataReaderExtensions
{
    public static string? GetNullableString(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static int? GetNullableInt(this SqlDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }
}
