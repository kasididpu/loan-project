using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// EF Core context for the CQRS Read database — a physically separate database
/// from the write side, kept in sync only through projected loan events. There
/// is deliberately no cross-database query anywhere: Azure SQL Database does not
/// support them, and the point of CQRS here is that the two sides share no
/// storage. Entities are configured inline (not by assembly scan) so the write
/// context's ApplyConfigurationsFromAssembly can never pull them in by mistake.
/// </summary>
public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options)
        : base(options)
    {
    }

    public DbSet<LoanReadModel> Loans => Set<LoanReadModel>();
    public DbSet<InstallmentReadModel> Installments => Set<InstallmentReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoanReadModel>(entity =>
        {
            entity.ToTable("loan_read_model");
            entity.HasKey(l => l.LoanId);
            entity.Property(l => l.LoanId).ValueGeneratedNever();

            entity.Property(l => l.Status).HasMaxLength(20);
            entity.Property(l => l.RateType).HasMaxLength(20);

            // decimal(18,2): satang precision, same as every money column.
            entity.Property(l => l.Principal).HasPrecision(18, 2);
            entity.Property(l => l.OutstandingBalance).HasPrecision(18, 2);
            entity.Property(l => l.TotalPaid).HasPrecision(18, 2);
            // A rate is a small fraction (0.120000) — six dp is ample.
            entity.Property(l => l.AnnualRate).HasPrecision(9, 6);

            // Portfolio summary filters by status; the index turns that scan
            // into a seek as the book grows.
            entity.HasIndex(l => l.Status).HasDatabaseName("IX_loan_read_model_Status");
        });

        modelBuilder.Entity<InstallmentReadModel>(entity =>
        {
            entity.ToTable("installment_read_model");
            entity.HasKey(i => new { i.LoanId, i.InstallmentNo });

            entity.Property(i => i.DueAmount).HasPrecision(18, 2);
            entity.Property(i => i.PaidAmount).HasPrecision(18, 2);

            // Daily collections groups by due date and, separately, by paid
            // date; one index each keeps both halves off a full table scan.
            entity.HasIndex(i => i.DueDateUtc).HasDatabaseName("IX_installment_read_model_DueDateUtc");
            entity.HasIndex(i => i.PaidAtUtc).HasDatabaseName("IX_installment_read_model_PaidAtUtc");
        });
    }
}
