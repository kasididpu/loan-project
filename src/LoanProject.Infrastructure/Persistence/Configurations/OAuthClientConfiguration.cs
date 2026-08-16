using LoanProject.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanProject.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping for the OAuth client registry. A unique index on ClientId backs the
/// single query pattern — look a client up by its id during the token exchange —
/// and enforces that no two clients share an id.
/// </summary>
internal sealed class OAuthClientConfiguration : IEntityTypeConfiguration<OAuthClient>
{
    public void Configure(EntityTypeBuilder<OAuthClient> builder)
    {
        builder.ToTable("OAuthClient");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.ClientId).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.ClientId).IsUnique();

        builder.Property(c => c.ClientSecretHash).HasMaxLength(256).IsRequired();
        builder.Property(c => c.Role).HasMaxLength(50).IsRequired();
        builder.Property(c => c.DisplayName).HasMaxLength(200);
    }
}
