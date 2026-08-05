using Library.Application.Contracts;
using Library.Domain;
using Library.Domain.Entities;

namespace Library.Application.Loans;

/// <summary>
/// Single place where a loan becomes a <see cref="LoanSummary"/>, so overdue status
/// and fees are computed identically wherever a loan is shown.
/// </summary>
public static class LoanMapping
{
    public static LoanSummary ToSummary(this Loan loan, string bookTitle, string barcode, DateTime nowUtc)
    {
        var daysOverdue = loan.DaysOverdue(nowUtc);

        return new LoanSummary(
            loan.Id,
            loan.BookCopyId,
            barcode,
            loan.MemberId,
            loan.Member?.FullName ?? "Unknown member",
            bookTitle,
            loan.CheckedOutAtUtc,
            loan.DueAtUtc,
            loan.ReturnedAtUtc,
            loan.IsOverdue(nowUtc),
            daysOverdue,
            LoanPolicy.CalculateOverdueFee(daysOverdue));
    }

    /// <summary>Overload for loans loaded with their copy and title already attached.</summary>
    public static LoanSummary ToSummary(this Loan loan, DateTime nowUtc) =>
        loan.ToSummary(
            loan.BookCopy?.Book?.Title ?? "Unknown title",
            loan.BookCopy?.Barcode ?? "-",
            nowUtc);
}
