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
    }
}
