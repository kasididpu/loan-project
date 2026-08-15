namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// Denormalized current-state row for one loan, rebuilt from the loan-events
/// stream by <see cref="LoanReadModelProjector"/>. Mutable by design: unlike the
/// domain entities, a read model exists to be overwritten as events arrive.
/// LastProjectedVersion is the idempotency handle — an event whose Version is
/// not greater than it has already been applied.
/// </summary>
public sealed class LoanReadModel
{
    public Guid LoanId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Principal { get; set; }
    public decimal AnnualRate { get; set; }
    public string RateType { get; set; } = string.Empty;
    public int TermMonths { get; set; }

    public decimal OutstandingBalance { get; set; }
    public int NextInstallmentNo { get; set; }
    public DateTime? NextDueDateUtc { get; set; }

    public decimal TotalPaid { get; set; }
    public int InstallmentsPaid { get; set; }

    public DateTime OriginatedAtUtc { get; set; }
    public DateTime? DisbursedAtUtc { get; set; }
    public DateTime? SettledAtUtc { get; set; }
    public DateTime? DefaultedAtUtc { get; set; }

    /// <summary>Highest event version already folded into this row (dedupe key).</summary>
    public int LastProjectedVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
