using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Library.Infrastructure.Persistence.Configurations;

public class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("Members");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.FullName).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Email).IsRequired().HasMaxLength(256);
        builder.Property(m => m.MembershipCode).IsRequired().HasMaxLength(20);

        builder.HasIndex(m => m.MembershipCode)
            .IsUnique()
            .HasDatabaseName("UX_Members_MembershipCode");

        builder.HasIndex(m => m.Email)
            .IsUnique()
            .HasDatabaseName("UX_Members_Email");
    }
}
