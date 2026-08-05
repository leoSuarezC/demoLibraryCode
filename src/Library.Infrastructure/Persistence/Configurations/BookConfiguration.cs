using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(400);
        builder.Property(b => b.Author).IsRequired().HasMaxLength(300);
        builder.Property(b => b.Isbn).HasMaxLength(13);
        builder.Property(b => b.Synopsis).HasMaxLength(4000);
        builder.Property(b => b.Category).HasMaxLength(100);
        builder.Property(b => b.Tags).HasMaxLength(500);

        // Two editions of the same work legitimately share a title and author, so the
        // catalogue does not force those to be unique. An ISBN, when present, is by
        // definition one edition - the filtered index enforces that without blocking
        // the many older titles that have no ISBN at all.
        builder.HasIndex(b => b.Isbn)
            .IsUnique()
            .HasFilter("[Isbn] IS NOT NULL")
            .HasDatabaseName("UX_Books_Isbn");

        // Supports the ORDER BY behind the default catalogue listing.
        builder.HasIndex(b => b.Title).HasDatabaseName("IX_Books_Title");
        builder.HasIndex(b => b.Author).HasDatabaseName("IX_Books_Author");
        builder.HasIndex(b => b.Category).HasDatabaseName("IX_Books_Category");

        // The domain exposes copies as a read-only collection; EF writes through the
        // backing field so the invariants on Book.AddCopy cannot be bypassed.
        builder.Metadata
            .FindNavigation(nameof(Book.Copies))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(b => b.Copies)
            .WithOne(c => c.Book!)
            .HasForeignKey(c => c.BookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
