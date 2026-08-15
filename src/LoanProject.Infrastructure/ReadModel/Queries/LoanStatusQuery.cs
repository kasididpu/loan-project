using LoanProject.Application.Loans;
using Microsoft.EntityFrameworkCore;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>Serves the loan status view from the Read DB — no tracking, read-only.</summary>
public sealed class LoanStatusQuery : ILoanStatusQuery
{
    private readonly ReadDbContext _db;

    public LoanStatusQuery(ReadDbContext db) => _db = db;

    public async Task<LoanStatusView?> GetAsync(Guid loanId, CancellationToken cancellationToken)
    {
        var loan = await _db.Loans
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoanId == loanId, cancellationToken);

        return loan is null
            ? null
            : new LoanStatusView(
                loan.LoanId, loan.CustomerId, loan.Status, loan.Principal, loan.AnnualRate,
                loan.RateType, loan.TermMonths, loan.OutstandingBalance, loan.NextInstallmentNo,
                loan.NextDueDateUtc, loan.TotalPaid, loan.InstallmentsPaid, loan.LastProjectedVersion);
    }
}
