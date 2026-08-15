using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.Streaming;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// Applies one projected loan event to the Read database. The write model is the
/// source of truth; this rebuilds a denormalized view from the same events,
/// interpreting each with the same pure calculators the aggregate uses so the
/// numbers match exactly. Idempotent: an event already reflected in
/// LastProjectedVersion is a no-op — which is what makes Redpanda's at-least-once
/// delivery safe. One SaveChanges = one transaction, so the effects and the new
/// LastProjectedVersion commit together or not at all.
/// </summary>
public sealed class LoanReadModelProjection
{
    private readonly ReadDbContext _db;

    public LoanReadModelProjection(ReadDbContext db) => _db = db;

    public async Task ProjectAsync(LoanEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.LoanId == envelope.AggregateId, cancellationToken);

        // Dedupe by (AggregateId, Version): the row already carries this event
        // (or a later one). Origination is the only event with no prior row.
        if (loan is not null && envelope.Version <= loan.LastProjectedVersion)
            return;

        switch (envelope.EventType)
        {
            case "LoanOriginated":
                loan = BuildOriginated(envelope);
                _db.Loans.Add(loan);
                break;
            case "LoanApproved":
                Require(loan, envelope).Status = nameof(LoanStatus.Approved);
                break;
            case "LoanRejected":
                Require(loan, envelope).Status = nameof(LoanStatus.Rejected);
                break;
            case "LoanDisbursed":
                ApplyDisbursed(Require(loan, envelope), envelope);
                break;
            case "PaymentReceived":
                await ApplyPaymentReceivedAsync(Require(loan, envelope), envelope, cancellationToken);
                break;
            case "LoanSettled":
                ApplySettled(Require(loan, envelope), envelope);
                break;
            case "LoanDefaulted":
                ApplyDefaulted(Require(loan, envelope), envelope);
                break;
            default:
                throw new NotSupportedException($"Unknown event type '{envelope.EventType}' on loan-events.");
        }

        loan!.LastProjectedVersion = envelope.Version;
        loan.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static LoanReadModel BuildOriginated(LoanEventEnvelope envelope)
    {
        var data = envelope.EventData;
        return new LoanReadModel
        {
            LoanId = envelope.AggregateId,
            CustomerId = data.GetProperty("CustomerId").GetGuid(),
            Status = nameof(LoanStatus.Originated),
            Principal = data.GetProperty("Principal").GetDecimal(),
            AnnualRate = data.GetProperty("AnnualRate").GetDecimal(),
            // Enums ride the wire as names (JsonStringEnumConverter), e.g. "Effective".
            RateType = data.GetProperty("RateType").GetString()!,
            TermMonths = data.GetProperty("TermMonths").GetInt32(),
            OutstandingBalance = 0m,
            NextInstallmentNo = 1,
            OriginatedAtUtc = envelope.OccurredAtUtc,
        };
    }

    private void ApplyDisbursed(LoanReadModel loan, LoanEventEnvelope envelope)
    {
        loan.Status = nameof(LoanStatus.Active);
        loan.OutstandingBalance = envelope.EventData.GetProperty("DisbursedAmount").GetDecimal();
        loan.DisbursedAtUtc = envelope.OccurredAtUtc;

        // The schedule is pure and carries no dates, so the read side derives
        // them the one place they are needed: installment N falls due N months
        // after the money went out.
        foreach (var installment in BuildSchedule(loan))
        {
            _db.Installments.Add(new InstallmentReadModel
            {
                LoanId = loan.LoanId,
                InstallmentNo = installment.Number,
                DueDateUtc = envelope.OccurredAtUtc.AddMonths(installment.Number),
                DueAmount = installment.Payment,
                Paid = false,
            });
        }

        loan.NextDueDateUtc = envelope.OccurredAtUtc.AddMonths(1);
    }

    private async Task ApplyPaymentReceivedAsync(
        LoanReadModel loan, LoanEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var installmentNo = envelope.EventData.GetProperty("InstallmentNo").GetInt32();
        var amount = envelope.EventData.GetProperty("Amount").GetDecimal();

        var installment = await _db.Installments.FirstOrDefaultAsync(
            i => i.LoanId == loan.LoanId && i.InstallmentNo == installmentNo, cancellationToken);
        if (installment is not null)
        {
            installment.Paid = true;
            installment.PaidAtUtc = envelope.OccurredAtUtc;
            installment.PaidAmount = amount;
        }

        // Outstanding balance mirrors the aggregate exactly: the remaining
        // balance of the schedule row just paid (same calculator, same inputs).
        loan.OutstandingBalance = BuildSchedule(loan)[installmentNo - 1].RemainingBalance;
        loan.NextInstallmentNo = installmentNo + 1;
        loan.InstallmentsPaid += 1;
        loan.TotalPaid += amount;
        loan.NextDueDateUtc = installmentNo < loan.TermMonths
            ? loan.DisbursedAtUtc!.Value.AddMonths(installmentNo + 1)
            : null;
    }

    private static void ApplySettled(LoanReadModel loan, LoanEventEnvelope envelope)
    {
        loan.Status = nameof(LoanStatus.Settled);
        loan.SettledAtUtc = envelope.OccurredAtUtc;
        loan.OutstandingBalance = 0m;
        loan.NextDueDateUtc = null;
    }

    private static void ApplyDefaulted(LoanReadModel loan, LoanEventEnvelope envelope)
    {
        loan.Status = nameof(LoanStatus.Defaulted);
        loan.DefaultedAtUtc = envelope.OccurredAtUtc;
    }

    private static IReadOnlyList<Installment> BuildSchedule(LoanReadModel loan) =>
        Enum.Parse<RateType>(loan.RateType) == RateType.Flat
            ? AmortizationCalculator.BuildFlatSchedule(loan.Principal, loan.AnnualRate, loan.TermMonths)
            : AmortizationCalculator.BuildSchedule(loan.Principal, loan.AnnualRate, loan.TermMonths);

    // Per-loan events arrive in version order (single partition keyed by
    // AggregateId), so a non-origination event always finds its row. If it does
    // not, the stream is corrupt and failing loud beats a silent wrong balance.
    private static LoanReadModel Require(LoanReadModel? loan, LoanEventEnvelope envelope) =>
        loan ?? throw new InvalidOperationException(
            $"Event '{envelope.EventType}' v{envelope.Version} arrived for loan {envelope.AggregateId} "
            + "before its LoanOriginated.");
}
