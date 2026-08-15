using System.Globalization;
using LoanProject.Application.Audit;
using LoanProject.Application.Rates;
using LoanProject.Domain.Loans;

namespace LoanProject.Application.Loans;

/// <summary>
/// Command side: mints a new loan. The interest rate is not taken from the
/// caller — it is looked up from the rate source (Redis-cached in Phase 5) for
/// the requested product, so the number on the loan always matches the sheet.
/// Appending LoanOriginated is all this does: the dispatcher, not this handler,
/// publishes it onward. A large principal also raises an AML review flag in the
/// audit log for a human to check (Phase 7) — advisory, it never blocks the loan.
/// </summary>
public sealed class OriginateLoanHandler
{
    // AML rule (simple + explainable): a principal at or above this raises a
    // review flag. A flat threshold, not a per-customer pattern — deliberately
    // the simplest rule that is still easy to reason about and test.
    public const decimal AmlReviewThresholdBaht = 1_000_000m;

    private readonly ILoanRepository _loans;
    private readonly IInterestRateLookup _rates;
    private readonly IAuditLogWriter _audit;

    public OriginateLoanHandler(ILoanRepository loans, IInterestRateLookup rates, IAuditLogWriter audit)
    {
        _loans = loans;
        _rates = rates;
        _audit = audit;
    }

    public async Task<Guid> HandleAsync(
        Guid customerId, decimal principal, RateType rateType, int termMonths, CancellationToken cancellationToken)
    {
        var annualRate = await _rates.GetAnnualRateAsync(rateType, termMonths, cancellationToken);

        var loanId = Guid.NewGuid();
        var loan = Loan.Originate(loanId, customerId, principal, annualRate, rateType, termMonths, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);

        if (principal >= AmlReviewThresholdBaht)
            await FlagForAmlReviewAsync(loanId, customerId, principal, cancellationToken);

        return loanId;
    }

    private Task FlagForAmlReviewAsync(
        Guid loanId, Guid customerId, decimal principal, CancellationToken cancellationToken) =>
        _audit.WriteAsync(
            new AuditEntry(
                "Loan",
                loanId.ToString(),
                "AmlReviewFlagged",
                "system",
                DateTime.UtcNow,
                // Money kept as invariant strings — the audit store (Mongo) has a
                // decimal-BSON trap; same convention as the settlement job.
                new Dictionary<string, object?>
                {
                    ["customerId"] = customerId.ToString(),
                    ["principal"] = principal.ToString(CultureInfo.InvariantCulture),
                    ["thresholdBaht"] = AmlReviewThresholdBaht.ToString(CultureInfo.InvariantCulture),
                    ["reason"] = "Principal at or above the AML review threshold.",
                }),
            cancellationToken);
}
