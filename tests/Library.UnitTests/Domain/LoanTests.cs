using Library.Domain;
using Library.Domain.Entities;

namespace Library.UnitTests.Domain;

public class LoanTests
{
    private static readonly DateTime CheckedOut = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private static Loan ALoan() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "key-1", CheckedOut, LoanPolicy.LoanPeriod);

    [Fact]
    public void Due_date_follows_the_loan_period()
    {
        var loan = ALoan();

        Assert.Equal(CheckedOut.AddDays(21), loan.DueAtUtc);
    }

    [Fact]
    public void A_loan_without_an_idempotency_key_is_refused() =>
        Assert.Throws<DomainException>(() =>
            new Loan(Guid.NewGuid(), Guid.NewGuid(), "  ", CheckedOut, LoanPolicy.LoanPeriod));

    [Fact]
    public void A_loan_within_its_term_is_not_overdue()
    {
        var loan = ALoan();

        Assert.False(loan.IsOverdue(CheckedOut.AddDays(20)));
        Assert.Equal(0, loan.DaysOverdue(CheckedOut.AddDays(20)));
    }

    [Fact]
    public void Days_overdue_counts_whole_days_past_the_due_date()
    {
        var loan = ALoan();

        Assert.Equal(4, loan.DaysOverdue(CheckedOut.AddDays(25)));
    }

    [Fact]
    public void A_returned_loan_is_never_overdue_however_late_it_was()
    {
        var loan = ALoan();
        loan.Return(CheckedOut.AddDays(40));

        Assert.False(loan.IsOverdue(CheckedOut.AddDays(90)));
        Assert.Equal(0, loan.DaysOverdue(CheckedOut.AddDays(90)));
    }

    [Fact]
    public void A_loan_cannot_be_returned_twice()
    {
        var loan = ALoan();
        loan.Return(CheckedOut.AddDays(3));

        Assert.Throws<DomainException>(() => loan.Return(CheckedOut.AddDays(4)));
    }

    [Fact]
    public void A_book_cannot_be_returned_before_it_was_borrowed()
    {
        var loan = ALoan();

        Assert.Throws<DomainException>(() => loan.Return(CheckedOut.AddDays(-1)));
    }
}

public class BookCopyTests
{
    private static BookCopy ACopy()
    {
        var book = new Book("Dune", "Frank Herbert");
        return book.AddCopy("LIB-00001");
    }

    [Fact]
    public void A_copy_starts_on_the_shelf()
    {
        var copy = ACopy();

        Assert.Equal(CopyStatus.Available, copy.Status);
        Assert.True(copy.IsAvailable);
    }

    [Fact]
    public void A_copy_already_on_loan_cannot_be_lent_again()
    {
        var copy = ACopy();
        copy.MarkOnLoan();

        Assert.Throws<DomainException>(copy.MarkOnLoan);
    }

    [Fact]
    public void A_copy_that_is_not_out_cannot_be_returned()
    {
        var copy = ACopy();

        Assert.Throws<DomainException>(copy.MarkReturned);
    }

    [Fact]
    public void Lending_and_returning_puts_the_copy_back_on_the_shelf()
    {
        var copy = ACopy();

        copy.MarkOnLoan();
        copy.MarkReturned();

        Assert.True(copy.IsAvailable);
    }

    [Fact]
    public void A_copy_on_loan_cannot_be_withdrawn_from_circulation()
    {
        var copy = ACopy();
        copy.MarkOnLoan();

        var refusal = Assert.Throws<DomainException>(() => copy.Withdraw(CopyStatus.Lost));
        Assert.Contains("check it in", refusal.Message);
    }

    [Fact]
    public void Withdrawing_requires_a_terminal_reason()
    {
        var copy = ACopy();

        Assert.Throws<DomainException>(() => copy.Withdraw(CopyStatus.Available));
    }
}
