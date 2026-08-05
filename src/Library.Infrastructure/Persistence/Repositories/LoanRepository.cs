using System.Data;
using Library.Application.Abstractions;
using Library.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Library.Infrastructure.Persistence.Repositories;

public class LoanRepository(LibraryDbContext db) : ILoanRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Deliberately drops below EF Core. Reserving a copy is a read-modify-write that
    /// must be serialised against other callers; expressing it as a stored procedure
    /// keeps the decision and the lock in the same place, one round trip, inside a
    /// transaction the database controls.
    /// </remarks>
    public async Task<CheckoutCommandResult> CheckoutAsync(
        Guid bookId,
        Guid memberId,
        string idempotencyKey,
        int loanDays,
        int maxConcurrentLoans,
        CancellationToken ct = default)
    {
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
            command.CommandText = "dbo.usp_CheckoutCopy";

            command.Parameters.Add(new SqlParameter("@BookId", SqlDbType.UniqueIdentifier) { Value = bookId });
            command.Parameters.Add(new SqlParameter("@MemberId", SqlDbType.UniqueIdentifier) { Value = memberId });
            command.Parameters.Add(new SqlParameter("@IdempotencyKey", SqlDbType.NVarChar, 100) { Value = idempotencyKey });
            command.Parameters.Add(new SqlParameter("@LoanDays", SqlDbType.Int) { Value = loanDays });
            command.Parameters.Add(new SqlParameter("@MaxConcurrentLoans", SqlDbType.Int) { Value = maxConcurrentLoans });

            var outcomeParam = new SqlParameter("@Outcome", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var loanIdParam = new SqlParameter("@LoanId", SqlDbType.UniqueIdentifier) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outcomeParam);
            command.Parameters.Add(loanIdParam);

            await command.ExecuteNonQueryAsync(ct);

            var outcome = (CheckoutOutcome)(int)outcomeParam.Value;
            var loanId = loanIdParam.Value is DBNull ? (Guid?)null : (Guid)loanIdParam.Value;

            // The procedure wrote rows behind EF's back, so anything already tracked
            // is now stale. Clearing keeps a later read in the same request honest.
            db.ChangeTracker.Clear();

            return new CheckoutCommandResult(outcome, loanId);
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
    }

    public Task<Loan?> GetOpenLoanForCopyAsync(Guid bookCopyId, CancellationToken ct = default) =>
        db.Loans
            .Include(l => l.BookCopy).ThenInclude(c => c!.Book)
            .Include(l => l.Member)
            .FirstOrDefaultAsync(l => l.BookCopyId == bookCopyId && l.ReturnedAtUtc == null, ct);

    public Task<Loan?> GetByIdAsync(Guid loanId, CancellationToken ct = default) =>
        db.Loans
            .Include(l => l.BookCopy).ThenInclude(c => c!.Book)
            .Include(l => l.Member)
            .FirstOrDefaultAsync(l => l.Id == loanId, ct);

    public async Task<IReadOnlyList<Loan>> GetOpenLoansAsync(int limit = 100, CancellationToken ct = default) =>
        await db.Loans
            .Include(l => l.BookCopy).ThenInclude(c => c!.Book)
            .Include(l => l.Member)
            .Where(l => l.ReturnedAtUtc == null)
            .OrderBy(l => l.DueAtUtc)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Loan>> GetOpenLoansForMemberAsync(
        Guid memberId,
        CancellationToken ct = default) =>
        await db.Loans
            .Include(l => l.BookCopy).ThenInclude(c => c!.Book)
            .Include(l => l.Member)
            .Where(l => l.MemberId == memberId && l.ReturnedAtUtc == null)
            .OrderBy(l => l.DueAtUtc)
            .ToListAsync(ct);
}
