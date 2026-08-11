using LoanProject.Domain.Customers;
using LoanProject.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the conventional (non-event-sourced) side of the write
/// database. The Loan aggregate is intentionally absent here: its state lives
/// in the append-only EventStore table and is accessed through the event
/// store repository, never through an ORM mapping.
/// </summary>
public sealed class LoanDbContext : DbContext
{
    public LoanDbContext(DbContextOptions<LoanDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(LoanDbContext).Assembly);
}
