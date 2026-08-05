using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class BookCopyConfiguration : IEntityTypeConfiguration<BookCopy>
{
    public void Configure(EntityTypeBuilder<BookCopy> builder)
    {
        builder.ToTable("BookCopies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Barcode).IsRequired().HasMaxLength(50);
        builder.Property(c => c.Status).HasConversion<int>();

        // A barcode identifies one physical item across the entire library.
        builder.HasIndex(c => c.Barcode)
            .IsUnique()
            .HasDatabaseName("UX_BookCopies_Barcode");

        // usp_CheckoutCopy looks for the lendable copies of one title; this is the
        // index that lookup rides on.
        builder.HasIndex(c => new { c.BookId, c.Status })
            .HasDatabaseName("IX_BookCopies_BookId_Status");

        builder.Metadata
            .FindNavigation(nameof(BookCopy.Loans))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.Loans)
            .WithOne(l => l.BookCopy!)
            .HasForeignKey(l => l.BookCopyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
