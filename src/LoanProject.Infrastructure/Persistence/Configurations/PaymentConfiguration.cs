using LoanProject.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoanProject.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payment");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // decimal(18,2): satang is the smallest unit ever stored — the same
        // resolution the domain enforces with its sub-satang guard.
        builder.Property(p => p.Amount).HasPrecision(18, 2);

        // No foreign key on purpose: the Loan aggregate has no relational
        // table — it lives in the append-only EventStore. Integrity of LoanId
        // is enforced by the aggregate itself, which validates every payment
        // before it is recorded.
        builder.Property(p => p.LoanId);

        // Bounded so the column can be indexed later for webhook idempotency
        // (Stripe event ids are short prefixed tokens, far under 100 chars).
        builder.Property(p => p.StripeEventId).HasMaxLength(100);

        // Same reason as CustomerConfiguration: get-only properties must be
        // mapped explicitly — EF's convention only sees settable properties.
        builder.Property(p => p.PaidAtUtc);

        // Shaped exactly like the statement query (WHERE LoanId = @loanId
        // ORDER BY PaidAtUtc): the first column turns the scan into a seek,
        // the second hands rows back pre-sorted so the plan loses its Sort
        // operator. Evidence: docs/execution-plans (before/after capture).
        builder.HasIndex(p => new { p.LoanId, p.PaidAtUtc })
            .HasDatabaseName("IX_Payment_LoanId_PaidAtUtc");

        // One Stripe event, one payment — the webhook idempotency rule
        // (phase 4) enforced mechanically by the database, same referee
        // philosophy as UQ_EventStore_AggVer on the ledger.
        builder.HasIndex(p => p.StripeEventId)
            .IsUnique()
            .HasDatabaseName("UX_Payment_StripeEventId");
    }
}
