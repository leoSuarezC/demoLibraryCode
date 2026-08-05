namespace Library.Application.Contracts;

public record LoanSummary(
    Guid Id,
    Guid BookCopyId,
    string Barcode,
    Guid MemberId,
    string MemberName,
    string BookTitle,
    DateTime CheckedOutAtUtc,
    DateTime DueAtUtc,
    DateTime? ReturnedAtUtc,
    bool IsOverdue,
    int DaysOverdue,
    decimal OverdueFee);

/// <summary>
/// Borrow any available copy of a title. The caller names the title, not the copy:
/// picking which item leaves the shelf is the library's job, and doing it inside the
/// database is what keeps two simultaneous requests from claiming the same one.
/// </summary>
public record CheckoutRequest(Guid BookId, Guid MemberId);

/// <summary>
/// Result of a check-out.
/// </summary>
/// <param name="WasAlreadyProcessed">
/// True when the idempotency key matched an existing loan, i.e. this was a retry and
/// no second copy was lent. Surfaced so the UI can say "already checked out" instead
/// of silently pretending a fresh loan was created.
/// </param>
public record CheckoutResult(LoanSummary Loan, bool WasAlreadyProcessed);

public record CheckinRequest(Guid BookCopyId);

public record CheckinResult(LoanSummary Loan, decimal FeeCharged);
