namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// One scheduled installment per row — the denormalized grain the daily
/// collections report reads. The due side is written when the loan is
/// disbursed; the collected side is filled in when the matching PaymentReceived
/// is projected.
/// </summary>
public sealed class InstallmentReadModel
{
    public Guid LoanId { get; set; }
    public int InstallmentNo { get; set; }

    public DateTime DueDateUtc { get; set; }
    public decimal DueAmount { get; set; }

    public bool Paid { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public decimal? PaidAmount { get; set; }
}
