using LoanProject.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanProject.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping lives in Infrastructure so the Domain entity stays free of any EF
/// concern (clean architecture: dependencies point inward, Domain references
/// nothing).
/// </summary>
internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customer");

        builder.HasKey(c => c.Id);
        // Ids are minted by the domain at creation time, never by the database.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.FullName).HasMaxLength(200);

        // Get-only properties are invisible to EF's convention (it only
        // discovers properties with a setter), so every immutable property
        // must be mapped explicitly or constructor binding fails.
        builder.Property(c => c.CreatedAtUtc);

        // KYC status (phase 7) stored as its name, not a number — readable in the
        // DB and consistent with the string-enum convention used elsewhere. A
        // server default of "Pending" lets the column be added to a table that
        // already has customer rows (and matches the domain's own default).
        builder.Property(c => c.KycStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(KycStatus.Pending);
    }
}
