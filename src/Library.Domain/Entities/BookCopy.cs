namespace Library.Domain.Entities;

/// <summary>
/// A physical item on a shelf. This — not the title — is what a member actually
/// carries home, and therefore what check-out and check-in operate on.
/// </summary>
public class BookCopy
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid BookId { get; private set; }
    public Book? Book { get; private set; }

    /// <summary>Label printed on the spine sticker. Unique across the whole library.</summary>
    public string Barcode { get; private set; } = string.Empty;

    public CopyStatus Status { get; private set; } = CopyStatus.Available;
    public DateTime AcquiredAtUtc { get; private set; } = DateTime.UtcNow;

    private readonly List<Loan> _loans = [];
    public IReadOnlyCollection<Loan> Loans => _loans.AsReadOnly();

    private BookCopy() { }

    internal BookCopy(Guid bookId, string barcode)
    {
        BookId = bookId;
        Barcode = barcode;
    }

    public bool IsAvailable => Status == CopyStatus.Available;

    /// <summary>
    /// Moves the copy off the shelf.
    /// </summary>
    /// <remarks>
    /// This guard is the in-memory expression of the rule; the authoritative check
    /// lives in <c>usp_CheckoutCopy</c>, which re-tests it while holding an update
    /// lock. Two processes can both read "Available" here — only the database can
    /// settle who actually gets the copy.
    /// </remarks>
    public void MarkOnLoan()
    {
        if (Status != CopyStatus.Available)
            throw new DomainException($"Copy {Barcode} is {Status.ToString().ToLowerInvariant()} and cannot be lent.");

        Status = CopyStatus.OnLoan;
    }

    public void MarkReturned()
    {
        if (Status != CopyStatus.OnLoan)
            throw new DomainException($"Copy {Barcode} is not currently on loan.");

        Status = CopyStatus.Available;
    }

    /// <summary>Takes the copy out of circulation without deleting its loan history.</summary>
    public void Withdraw(CopyStatus reason)
    {
        if (reason is not (CopyStatus.Lost or CopyStatus.Withdrawn))
            throw new DomainException("A copy can only be withdrawn as lost or retired.");
        if (Status == CopyStatus.OnLoan)
            throw new DomainException($"Copy {Barcode} is on loan; check it in before withdrawing it.");

        Status = reason;
    }
}
