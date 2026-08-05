using Library.Domain.Entities;

namespace Library.Application.Abstractions;

/// <summary>Outcome codes returned by the check-out stored procedure.</summary>
public enum CheckoutOutcome
{
    /// <summary>A copy was reserved and a loan created.</summary>
    Created = 0,

    /// <summary>The idempotency key matched an existing loan; nothing new was lent.</summary>
    AlreadyProcessed = 1,

    /// <summary>The title exists but every copy is out.</summary>
    NoCopyAvailable = 2,

    /// <summary>The member is already holding the maximum number of items.</summary>
    LoanLimitReached = 3,

    /// <summary>No such title.</summary>
    BookNotFound = 4,

    /// <summary>No such member, or the membership is inactive.</summary>
    MemberNotFound = 5
}

public record CheckoutCommandResult(CheckoutOutcome Outcome, Guid? LoanId);

public interface ILoanRepository
{
    /// <summary>
    /// Reserves an available copy of <paramref name="bookId"/> and opens a loan, as a
    /// single atomic operation inside the database.
    /// </summary>
    /// <remarks>
    /// This is intentionally not composed from separate reads and writes. Selecting a
    /// free copy and then marking it lent leaves a window in which a second request
    /// selects the same copy; under load that window is hit routinely. The work is
    /// therefore delegated to <c>usp_CheckoutCopy</c>, which takes an update lock on
    /// the chosen row and re-verifies availability while holding it.
    /// </remarks>
    Task<CheckoutCommandResult> CheckoutAsync(
        Guid bookId,
        Guid memberId,
        string idempotencyKey,
        int loanDays,
        int maxConcurrentLoans,
        CancellationToken ct = default);

    /// <summary>Finds the open loan for a copy, if any.</summary>
    Task<Loan?> GetOpenLoanForCopyAsync(Guid bookCopyId, CancellationToken ct = default);

    Task<Loan?> GetByIdAsync(Guid loanId, CancellationToken ct = default);

    /// <summary>Open loans across the library, most overdue first. Drives the staff dashboard.</summary>
    Task<IReadOnlyList<Loan>> GetOpenLoansAsync(int limit = 100, CancellationToken ct = default);
}
