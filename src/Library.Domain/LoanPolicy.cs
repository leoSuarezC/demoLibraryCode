namespace Library.Domain;

/// <summary>
/// The lending rules of the library, gathered in one place so they can be read,
/// tested and changed without hunting through use cases.
/// </summary>
public static class LoanPolicy
{
    /// <summary>How long a member may keep a book.</summary>
    public static readonly TimeSpan LoanPeriod = TimeSpan.FromDays(21);

    /// <summary>Concurrent open loans allowed per member.</summary>
    public const int MaxConcurrentLoansPerMember = 5;

    /// <summary>Charge applied for each whole day past the due date.</summary>
    public const decimal DailyOverdueFee = 0.25m;

    /// <summary>A single overdue item never accrues more than this.</summary>
    public const decimal MaxFeePerLoan = 15.00m;

    /// <summary>
    /// Grace period before fees begin, so a member returning a book the morning
    /// after it fell due is not charged.
    /// </summary>
    public const int GraceDays = 1;

    /// <summary>
    /// Calculates what a member owes for an overdue item.
    /// </summary>
    /// <remarks>
    /// Ported from the legacy VB.NET billing module; see <c>legacy/</c> and the
    /// parity tests that pin this behaviour to the original implementation.
    /// </remarks>
    public static decimal CalculateOverdueFee(int daysOverdue)
    {
        if (daysOverdue <= GraceDays) return 0m;

        var chargeableDays = daysOverdue - GraceDays;
        var fee = chargeableDays * DailyOverdueFee;

        return Math.Min(fee, MaxFeePerLoan);
    }
}
