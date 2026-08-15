using LoanProject.Application.Rates;
using LoanProject.Domain.Loans;

namespace LoanProject.Application.Loans;

/// <summary>
/// Command side: mints a new loan. The interest rate is not taken from the
/// caller — it is looked up from the rate source (Redis-cached in Phase 5) for
/// the requested product, so the number on the loan always matches the sheet.
/// Appending LoanOriginated is all this does: the dispatcher, not this handler,
/// publishes it onward (CQRS — the command path never touches the read side or
/// the bus directly).
/// </summary>
public sealed class OriginateLoanHandler
{
    private readonly ILoanRepository _loans;
    private readonly IInterestRateLookup _rates;

    public OriginateLoanHandler(ILoanRepository loans, IInterestRateLookup rates)
    {
        _loans = loans;
        _rates = rates;
    }

    public async Task<Guid> HandleAsync(
        Guid customerId, decimal principal, RateType rateType, int termMonths, CancellationToken cancellationToken)
    {
        var annualRate = await _rates.GetAnnualRateAsync(rateType, termMonths, cancellationToken);

        var loanId = Guid.NewGuid();
        var loan = Loan.Originate(loanId, customerId, principal, annualRate, rateType, termMonths, DateTime.UtcNow);
        await _loans.SaveAsync(loan, cancellationToken);
        return loanId;
    }
}
