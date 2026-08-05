using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.IdempotencyKey).IsRequired().HasMaxLength(100);

        // ---------------------------------------------------------------------
        // The two indexes below are correctness constraints, not tuning.
        // ---------------------------------------------------------------------

        // Retry safety. A dropped response or an impatient double-click must not lend
        // a second copy. Enforcing this in the database rather than with a preceding
        // SELECT is what makes it hold under concurrency: two simultaneous retries can
        // both find nothing and both proceed, but only one can win this index.
        builder.HasIndex(l => l.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_Loans_IdempotencyKey");

        // A physical item can be in at most one pair of hands. The filtered index says
        // exactly that: any number of closed loans per copy, never two open ones. It
        // is the backstop that makes a bug in the check-out path fail loudly at the
        // database instead of quietly double-lending a book.
        builder.HasIndex(l => l.BookCopyId)
            .IsUnique()
            .HasFilter("[ReturnedAtUtc] IS NULL")
            .HasDatabaseName("UX_Loans_OpenLoanPerCopy");

        // Drives the overdue dashboard: open loans ordered by due date.
        builder.HasIndex(l => new { l.ReturnedAtUtc, l.DueAtUtc })
            .HasDatabaseName("IX_Loans_Open_DueAt");

        builder.HasIndex(l => l.MemberId).HasDatabaseName("IX_Loans_MemberId");

        builder.HasOne(l => l.Member!)
            .WithMany()
            .HasForeignKey(l => l.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
