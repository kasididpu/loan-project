namespace LoanProject.Domain.Loans;

/// <summary>
/// Reducing-balance schedule with a fixed installment (annuity).
///
/// Rounding strategy (documented per project money rules): every money value
/// is rounded to 2 decimals with MidpointRounding.AwayFromZero — the rounding
/// a person expects on a payment slip; predictability beats the tiny
/// statistical bias ToEven would remove. Drift accumulated over the first
/// n-1 installments is absorbed by the final installment, which pays the
/// exact remaining balance plus its own interest so the loan closes at 0.00.
/// </summary>
public static class AmortizationCalculator
{
    public static IReadOnlyList<Installment> BuildSchedule(decimal principal, decimal annualRate, int termMonths)
    {
        if (principal <= 0)
            throw new ArgumentOutOfRangeException(nameof(principal), principal, "Principal must be positive.");
        if (principal % 0.01m != 0m)
            throw new ArgumentException("Principal must not be finer than satang (2 decimal places).", nameof(principal));
        if (annualRate < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRate), annualRate, "Annual rate cannot be negative.");
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), termMonths, "Term must be at least one month.");

        var monthlyRate = annualRate / 12m;
        var payment = MonthlyPayment(principal, monthlyRate, termMonths);

        var rows = new List<Installment>(termMonths);
        var balance = principal;

        for (var number = 1; number < termMonths; number++)
        {
            var interest = RoundMoney(balance * monthlyRate);
            var principalPortion = payment - interest; // both are 2 dp, so this stays 2 dp
            balance -= principalPortion;
            rows.Add(new Installment(number, payment, interest, principalPortion, balance));
        }

        // Final installment: the exact remainder plus its interest — absorbs the
        // rounding drift of the earlier rows so the loan closes at exactly zero.
        var lastInterest = RoundMoney(balance * monthlyRate);
        rows.Add(new Installment(termMonths, balance + lastInterest, lastInterest, balance, 0m));

        return rows;
    }

    private static decimal MonthlyPayment(decimal principal, decimal monthlyRate, int termMonths)
    {
        // Zero-rate loans split the principal evenly — the annuity formula
        // below would divide by zero.
        if (monthlyRate == 0m)
            return RoundMoney(principal / termMonths);

        // (1+i)^n by repeated decimal multiplication: Math.Pow works in
        // double, which is forbidden for money.
        var compound = 1m;
        for (var month = 0; month < termMonths; month++)
            compound *= 1m + monthlyRate;

        return RoundMoney(principal * monthlyRate * compound / (compound - 1m));
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
