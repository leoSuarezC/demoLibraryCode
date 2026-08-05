namespace Library.Domain.Entities;

/// <summary>
/// One borrowing event: a member took a specific copy home on a specific day.
/// The loan is open while <see cref="ReturnedAtUtc"/> is null.
/// </summary>
public class Loan
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid BookCopyId { get; private set; }
    public BookCopy? BookCopy { get; private set; }

    public Guid MemberId { get; private set; }
    public Member? Member { get; private set; }

    public DateTime CheckedOutAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime DueAtUtc { get; private set; }
    public DateTime? ReturnedAtUtc { get; private set; }

    /// <summary>
    /// Caller-supplied key that makes check-out safe to retry.
    /// </summary>
    /// <remarks>
    /// A dropped response, an impatient double-click or a queue redelivery must not
    /// lend a second copy. The key is enforced by a unique index rather than by an
    /// application-level "check then insert", which would still race under concurrency.
    /// </remarks>
    public string IdempotencyKey { get; private set; } = string.Empty;

    private Loan() { }

    public Loan(Guid bookCopyId, Guid memberId, string idempotencyKey, DateTime checkedOutAtUtc, TimeSpan loanPeriod)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new DomainException("Check-out requires an idempotency key.");

        BookCopyId = bookCopyId;
        MemberId = memberId;
        IdempotencyKey = idempotencyKey.Trim();
        CheckedOutAtUtc = checkedOutAtUtc;
        DueAtUtc = checkedOutAtUtc.Add(loanPeriod);
    }

    public bool IsOpen => ReturnedAtUtc is null;

    public bool IsOverdue(DateTime asOfUtc) => IsOpen && asOfUtc > DueAtUtc;

    /// <summary>Whole days past the due date; zero while the loan is still within term.</summary>
    public int DaysOverdue(DateTime asOfUtc)
    {
        if (!IsOverdue(asOfUtc)) return 0;
        return (int)Math.Floor((asOfUtc - DueAtUtc).TotalDays);
    }

    public void Return(DateTime returnedAtUtc)
    {
        if (!IsOpen)
            throw new DomainException("This loan has already been closed.");
        if (returnedAtUtc < CheckedOutAtUtc)
            throw new DomainException("A book cannot be returned before it was borrowed.");

        ReturnedAtUtc = returnedAtUtc;
    }
}
