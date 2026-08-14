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
    }
}
