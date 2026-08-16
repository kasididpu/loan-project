using LoanProject.Application;
using LoanProject.Application.Security;
using LoanProject.Domain.Customers;
using LoanProject.Domain.Payments;
using LoanProject.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the conventional (non-event-sourced) side of the write
/// database. The Loan aggregate is intentionally absent here: its state lives
/// in the append-only EventStore table and is accessed through the event
/// store repository, never through an ORM mapping.
/// Implements IUnitOfWork directly: a DbContext already is one — the change
/// tracker collects the work and SaveChanges commits it as one transaction.
/// Derives from IdentityDbContext (Phase 8) so the ASP.NET Core Identity tables
/// (users, roles, ...) live in the same write database, keyed by Guid.
/// </summary>
public sealed class LoanDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>, IUnitOfWork
{
    private readonly IFieldEncryptor _fieldEncryptor;

    public LoanDbContext(DbContextOptions<LoanDbContext> options, IFieldEncryptor fieldEncryptor)
        : base(options)
        => _fieldEncryptor = fieldEncryptor;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own model (AspNetUsers, AspNetRoles, ...) must be built first.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanDbContext).Assembly);

        // Field-level encryption for PII (Phase 8): the value is encrypted on the
        // way to the column and decrypted on the way back, so nothing outside these
        // lines ever sees the stored ciphertext. Configured here rather than in
        // CustomerConfiguration because the conversion needs the injected encryptor.
        // EF never invokes a converter for a null value, so an unset field stays
        // null (the v! only satisfies the nullable-annotation checker).
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.Property(c => c.NationalId)
                .HasConversion(v => _fieldEncryptor.Encrypt(v!), v => _fieldEncryptor.Decrypt(v))
                .HasMaxLength(256);
            customer.Property(c => c.BankAccountNumber)
                .HasConversion(v => _fieldEncryptor.Encrypt(v!), v => _fieldEncryptor.Decrypt(v))
                .HasMaxLength(256);
        });
    }
}
