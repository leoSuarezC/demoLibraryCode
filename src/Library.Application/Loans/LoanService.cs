using Library.Application.Abstractions;
using Library.Application.Common;
using Library.Application.Contracts;
using Library.Domain;

namespace Library.Application.Loans;

/// <summary>
/// Lending and returning: the desk operations.
/// </summary>
public class LoanService(
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>
    /// Lends an available copy of a title.
    /// </summary>
    /// <param name="idempotencyKey">
    /// Supplied by the caller, normally from an <c>Idempotency-Key</c> header. Two
    /// requests carrying the same key produce one loan, however they interleave.
    /// </param>
    public async Task<Result<CheckoutResult>> CheckoutAsync(
        CheckoutRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Result<CheckoutResult>.Invalid("An Idempotency-Key header is required to check a book out.");

        if (idempotencyKey.Length > 100)
            return Result<CheckoutResult>.Invalid("Idempotency-Key must be 100 characters or fewer.");

        var outcome = await loans.CheckoutAsync(
            request.BookId,
            request.MemberId,
            idempotencyKey.Trim(),
            loanDays: (int)LoanPolicy.LoanPeriod.TotalDays,
            maxConcurrentLoans: LoanPolicy.MaxConcurrentLoansPerMember,
            ct);

        switch (outcome.Outcome)
        {
            case CheckoutOutcome.BookNotFound:
                return Result<CheckoutResult>.NotFound($"No book with id {request.BookId}.");

            case CheckoutOutcome.MemberNotFound:
                return Result<CheckoutResult>.NotFound(
                    $"No active member with id {request.MemberId}.");

            case CheckoutOutcome.NoCopyAvailable:
                return Result<CheckoutResult>.Conflict(
                    "Every copy of this title is currently on loan.");

            case CheckoutOutcome.LoanLimitReached:
                return Result<CheckoutResult>.Conflict(
                    $"This member already holds the maximum of {LoanPolicy.MaxConcurrentLoansPerMember} items.");

            case CheckoutOutcome.Created:
            case CheckoutOutcome.AlreadyProcessed:
                break;

            default:
                return Result<CheckoutResult>.Invalid($"Unrecognised check-out outcome '{outcome.Outcome}'.");
        }

        if (outcome.LoanId is not { } loanId)
            return Result<CheckoutResult>.Invalid("The check-out reported success without returning a loan.");

        var loan = await loans.GetByIdAsync(loanId, ct);
        if (loan is null)
            return Result<CheckoutResult>.NotFound($"Loan {loanId} could not be read back.");

        return Result<CheckoutResult>.Success(new CheckoutResult(
            loan.ToSummary(clock.UtcNow),
            WasAlreadyProcessed: outcome.Outcome == CheckoutOutcome.AlreadyProcessed));
    }

    /// <summary>
    /// Takes a copy back, closes its loan and reports any fee due.
    /// </summary>
    /// <remarks>
    /// Unlike check-out this needs no idempotency key: returning an already-returned
    /// copy is detectable from state alone, because a copy has at most one open loan.
    /// </remarks>
    public async Task<Result<CheckinResult>> CheckinAsync(
        CheckinRequest request,
        CancellationToken ct = default)
    {
        var loan = await loans.GetOpenLoanForCopyAsync(request.BookCopyId, ct);
        if (loan is null)
            return Result<CheckinResult>.NotFound(
                "That copy is not currently on loan.");

        var now = clock.UtcNow;
        var fee = LoanPolicy.CalculateOverdueFee(loan.DaysOverdue(now));

        try
        {
            loan.Return(now);
            loan.BookCopy!.MarkReturned();
        }
        catch (DomainException ex)
        {
            return Result<CheckinResult>.Conflict(ex.Message);
        }

        await unitOfWork.SaveChangesAsync(ct);

        return Result<CheckinResult>.Success(new CheckinResult(loan.ToSummary(now), fee));
    }

    public async Task<IReadOnlyList<LoanSummary>> GetOpenLoansAsync(int limit = 100, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var open = await loans.GetOpenLoansAsync(limit, ct);
        return open.Select(l => l.ToSummary(now)).ToList();
    }

    /// <summary>
    /// What one member is holding.
    /// </summary>
    /// <remarks>
    /// Takes the member id from the caller rather than reading it here, so the
    /// decision of <em>whose</em> loans may be seen stays in one place: the endpoint,
    /// which reads it off the token and never off the query string.
    /// </remarks>
    public async Task<IReadOnlyList<LoanSummary>> GetLoansForMemberAsync(
        Guid memberId,
        CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var open = await loans.GetOpenLoansForMemberAsync(memberId, ct);
        return open.Select(l => l.ToSummary(now)).ToList();
    }
}
