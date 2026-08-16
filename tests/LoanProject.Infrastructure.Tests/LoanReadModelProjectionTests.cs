using LoanProject.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Projection against the real Read DB (docker compose up). Fresh loan ids keep
/// every test isolated in the shared database.
/// </summary>
public class LoanReadModelProjectionTests
{
    private static readonly DateTime Origin = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProjectAsync_FullLifecycle_BuildsReadModelMatchingAggregate()
    {
        var loanId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var loan = Loan.Originate(loanId, customerId, 120_000m, 0.12m, RateType.Effective, 12, Origin);
        loan.Approve(Guid.NewGuid(), "officer", Origin);
        loan.Disburse(120_000m, Guid.NewGuid(), "officer", Origin);
        var firstPayment = loan.Schedule![0].Payment;
        loan.ReceivePayment(Guid.NewGuid(), firstPayment, 1, "evt_read_1", Origin.AddMonths(1));
        var expectedOutstanding = loan.OutstandingBalance;

        await ReadModelTesting.ProjectAllAsync(ReadModelTesting.Envelopes(loan));

        await using var db = TestReadDatabase.CreateContext();
        var row = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == loanId);

        Assert.NotNull(row);
        Assert.Equal("Active", row!.Status);
        Assert.Equal(customerId, row.CustomerId);
        Assert.Equal(120_000m, row.Principal);
        Assert.Equal("Effective", row.RateType);
        Assert.Equal(1, row.InstallmentsPaid);
        Assert.Equal(firstPayment, row.TotalPaid);
        Assert.Equal(2, row.NextInstallmentNo);
        // The read side rebuilds the balance with the same calculator, so it
        // equals the aggregate's own OutstandingBalance to the satang.
        Assert.Equal(expectedOutstanding, row.OutstandingBalance);
        Assert.Equal(4, row.LastProjectedVersion);
        Assert.Equal(Origin.AddMonths(2), row.NextDueDateUtc);

        var installments = await db.Installments.AsNoTracking()
            .Where(i => i.LoanId == loanId)
            .OrderBy(i => i.InstallmentNo)
            .ToListAsync();
        Assert.Equal(12, installments.Count);
        Assert.True(installments[0].Paid);
        Assert.Equal(firstPayment, installments[0].PaidAmount);
        Assert.Equal(Origin.AddMonths(1), installments[0].DueDateUtc);
        Assert.False(installments[1].Paid);
    }

    [Fact]
    public async Task ProjectAsync_ReappliedEvents_AreIdempotent()
    {
        var loanId = Guid.NewGuid();
        var loan = Loan.Originate(loanId, Guid.NewGuid(), 60_000m, 0.18m, RateType.Effective, 6, Origin);
        loan.Approve(Guid.NewGuid(), "officer", Origin);
        loan.Disburse(60_000m, Guid.NewGuid(), "officer", Origin);
        var firstPayment = loan.Schedule![0].Payment;
        loan.ReceivePayment(Guid.NewGuid(), firstPayment, 1, "evt_idem_1", Origin.AddMonths(1));
        var envelopes = ReadModelTesting.Envelopes(loan);

        await ReadModelTesting.ProjectAllAsync(envelopes);
        await ReadModelTesting.ProjectAllAsync(envelopes); // replay every event

        await using var db = TestReadDatabase.CreateContext();
        var row = await db.Loans.AsNoTracking().FirstAsync(l => l.LoanId == loanId);

        Assert.Equal(1, row.InstallmentsPaid);        // not doubled
        Assert.Equal(firstPayment, row.TotalPaid);    // not doubled
        Assert.Equal(4, row.LastProjectedVersion);

        var installmentCount = await db.Installments.CountAsync(i => i.LoanId == loanId);
        Assert.Equal(6, installmentCount);            // schedule not duplicated
    }
}
