using Library.Application.Abstractions;
using Library.Application.Common;
using Library.Application.Contracts;
using Library.Application.Loans;
using Library.Domain;
using Library.Domain.Entities;
using NSubstitute;

namespace Library.UnitTests.Application;

public class LoanServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ILoanRepository _loans = Substitute.For<ILoanRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private LoanService CreateService()
    {
        _clock.UtcNow.Returns(Now);
        return new LoanService(_loans, _unitOfWork, _clock);
    }

    /// <summary>Builds a loan wired to a copy and title, as the repository would return it.</summary>
    private static Loan AStoredLoan(DateTime checkedOutAt, string idempotencyKey = "key-1")
    {
        var book = new Book("Dune", "Frank Herbert");
        var copy = book.AddCopy("LIB-00001");
        copy.MarkOnLoan();

        var loan = new Loan(copy.Id, Guid.NewGuid(), idempotencyKey, checkedOutAt, LoanPolicy.LoanPeriod);

        // The navigation properties are what the mapper reads; the repository
        // populates them with Include, so the fixture does the same via reflection.
        typeof(Loan).GetProperty(nameof(Loan.BookCopy))!.SetValue(loan, copy);
        typeof(BookCopy).GetProperty(nameof(BookCopy.Book))!.SetValue(copy, book);

        return loan;
    }

    // ---------------------------------------------------------------- check-out

    [Fact]
    public async Task Check_out_without_an_idempotency_key_is_rejected()
    {
        var service = CreateService();

        var result = await service.CheckoutAsync(new CheckoutRequest(Guid.NewGuid(), Guid.NewGuid()), "");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Kind);
        await _loans.DidNotReceiveWithAnyArgs()
            .CheckoutAsync(default, default, default!, default, default);
    }

    [Fact]
    public async Task A_successful_check_out_reports_a_fresh_loan()
    {
        var loan = AStoredLoan(Now);
        _loans.CheckoutAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new CheckoutCommandResult(CheckoutOutcome.Created, loan.Id));
        _loans.GetByIdAsync(loan.Id).ReturnsForAnyArgs(loan);

        var service = CreateService();
        var result = await service.CheckoutAsync(new CheckoutRequest(Guid.NewGuid(), Guid.NewGuid()), "key-1");

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.WasAlreadyProcessed);
        Assert.Equal("Dune", result.Value.Loan.BookTitle);
    }

    [Fact]
    public async Task A_retry_carrying_the_same_key_returns_the_original_loan_and_says_so()
    {
        var loan = AStoredLoan(Now);
        _loans.CheckoutAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new CheckoutCommandResult(CheckoutOutcome.AlreadyProcessed, loan.Id));
        _loans.GetByIdAsync(loan.Id).ReturnsForAnyArgs(loan);

        var service = CreateService();
        var result = await service.CheckoutAsync(new CheckoutRequest(Guid.NewGuid(), Guid.NewGuid()), "key-1");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.WasAlreadyProcessed);
        Assert.Equal(loan.Id, result.Value.Loan.Id);
    }

    [Theory]
    [InlineData(CheckoutOutcome.NoCopyAvailable, ErrorKind.Conflict)]
    [InlineData(CheckoutOutcome.LoanLimitReached, ErrorKind.Conflict)]
    [InlineData(CheckoutOutcome.BookNotFound, ErrorKind.NotFound)]
    [InlineData(CheckoutOutcome.MemberNotFound, ErrorKind.NotFound)]
    public async Task Refusals_from_the_procedure_map_to_the_right_error_kind(
        CheckoutOutcome outcome,
        ErrorKind expected)
    {
        _loans.CheckoutAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new CheckoutCommandResult(outcome, null));

        var service = CreateService();
        var result = await service.CheckoutAsync(new CheckoutRequest(Guid.NewGuid(), Guid.NewGuid()), "key-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Kind);
    }

    [Fact]
    public async Task The_configured_loan_terms_are_passed_to_the_procedure()
    {
        var loan = AStoredLoan(Now);
        _loans.CheckoutAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new CheckoutCommandResult(CheckoutOutcome.Created, loan.Id));
        _loans.GetByIdAsync(loan.Id).ReturnsForAnyArgs(loan);

        var service = CreateService();
        await service.CheckoutAsync(new CheckoutRequest(Guid.NewGuid(), Guid.NewGuid()), "  key-1  ");

        await _loans.Received(1).CheckoutAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            "key-1",                                          // trimmed
            (int)LoanPolicy.LoanPeriod.TotalDays,
            LoanPolicy.MaxConcurrentLoansPerMember,
            Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------- check-in

    [Fact]
    public async Task Checking_in_a_copy_that_is_not_out_reports_not_found()
    {
        _loans.GetOpenLoanForCopyAsync(default).ReturnsForAnyArgs((Loan?)null);

        var service = CreateService();
        var result = await service.CheckinAsync(new CheckinRequest(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task Checking_in_on_time_closes_the_loan_with_no_fee()
    {
        var loan = AStoredLoan(Now.AddDays(-3));
        _loans.GetOpenLoanForCopyAsync(default).ReturnsForAnyArgs(loan);

        var service = CreateService();
        var result = await service.CheckinAsync(new CheckinRequest(loan.BookCopyId));

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.FeeCharged);
        Assert.Equal(Now, loan.ReturnedAtUtc);
        Assert.True(loan.BookCopy!.IsAvailable);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Checking_in_late_charges_the_overdue_fee_and_still_returns_the_copy()
    {
        // 21-day term, borrowed 25 days ago -> 4 days late, 3 chargeable after grace.
        var loan = AStoredLoan(Now.AddDays(-25));
        _loans.GetOpenLoanForCopyAsync(default).ReturnsForAnyArgs(loan);

        var service = CreateService();
        var result = await service.CheckinAsync(new CheckinRequest(loan.BookCopyId));

        Assert.True(result.IsSuccess);
        Assert.Equal(0.75m, result.Value.FeeCharged);
        Assert.True(loan.BookCopy!.IsAvailable);
    }

    [Fact]
    public async Task Nothing_is_committed_when_check_in_is_refused()
    {
        _loans.GetOpenLoanForCopyAsync(default).ReturnsForAnyArgs((Loan?)null);

        var service = CreateService();
        await service.CheckinAsync(new CheckinRequest(Guid.NewGuid()));

        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }
}
