using Library.Domain;

namespace Library.UnitTests.Domain;

/// <summary>
/// Fee rules. These are the numbers a member will argue about at the desk, so they
/// are pinned rather than left to whatever the arithmetic happens to produce.
/// </summary>
public class LoanPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]   // returned within the grace day
    public void No_fee_is_charged_within_the_grace_period(int daysOverdue) =>
        Assert.Equal(0m, LoanPolicy.CalculateOverdueFee(daysOverdue));

    [Theory]
    [InlineData(2, 0.25)]    // 1 chargeable day
    [InlineData(3, 0.50)]
    [InlineData(11, 2.50)]
    public void Fee_accrues_per_day_after_the_grace_period(int daysOverdue, decimal expected) =>
        Assert.Equal(expected, LoanPolicy.CalculateOverdueFee(daysOverdue));

    [Fact]
    public void Fee_is_capped_so_a_lost_book_cannot_accrue_indefinitely()
    {
        var afterOneYear = LoanPolicy.CalculateOverdueFee(365);

        Assert.Equal(LoanPolicy.MaxFeePerLoan, afterOneYear);
    }

    [Fact]
    public void Negative_days_are_treated_as_not_overdue() =>
        Assert.Equal(0m, LoanPolicy.CalculateOverdueFee(-5));
}
