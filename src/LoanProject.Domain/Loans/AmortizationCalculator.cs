namespace LoanProject.Domain.Loans;

/// <summary>
/// Loan schedule calculators: reducing-balance (annuity) and flat-rate.
///
/// Rounding strategy (documented per project money rules): every money value
/// is rounded to 2 decimals with MidpointRounding.AwayFromZero — the rounding
/// a person expects on a payment slip; predictability beats the tiny
/// statistical bias ToEven would remove. Drift accumulated over the first
/// n-1 installments is absorbed by the final installment, which pays the
/// exact remainder so the loan always closes at 0.00.
/// </summary>
public static class AmortizationCalculator
{
    /// <summary>
    /// Reducing balance: interest accrues on the remaining balance, the
    /// installment is fixed (annuity formula).
    /// </summary>
    public static IReadOnlyList<Installment> BuildSchedule(decimal principal, decimal annualRate, int termMonths)
    {
        ValidateInputs(principal, annualRate, termMonths);

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

        var lastInterest = RoundMoney(balance * monthlyRate);
        rows.Add(new Installment(termMonths, balance + lastInterest, lastInterest, balance, 0m));

        return rows;
    }

    /// <summary>
    /// Flat rate: interest is charged on the ORIGINAL principal for every
    /// installment, regardless of repayments — which is why a flat rate is
    /// roughly twice as expensive as the same reducing-balance rate. The
    /// final installment absorbs both the principal and interest drift so
    /// the totals match the flat formula to the satang.
    /// </summary>
    public static IReadOnlyList<Installment> BuildFlatSchedule(decimal principal, decimal annualRate, int termMonths)
    {
        ValidateInputs(principal, annualRate, termMonths);

        var totalInterest = RoundMoney(principal * annualRate * termMonths / 12m);
        var payment = RoundMoney((principal + totalInterest) / termMonths);
        var monthlyInterest = RoundMoney(totalInterest / termMonths);

        var rows = new List<Installment>(termMonths);
        var balance = principal;

        for (var number = 1; number < termMonths; number++)
        {
            var principalPortion = payment - monthlyInterest;
            balance -= principalPortion;
            rows.Add(new Installment(number, payment, monthlyInterest, principalPortion, balance));
        }

        var lastInterest = totalInterest - monthlyInterest * (termMonths - 1);
        rows.Add(new Installment(termMonths, balance + lastInterest, lastInterest, balance, 0m));

        return rows;
    }

    private static void ValidateInputs(decimal principal, decimal annualRate, int termMonths)
    {
        if (principal <= 0)
            throw new ArgumentOutOfRangeException(nameof(principal), principal, "Principal must be positive.");
        if (principal % 0.01m != 0m)
            throw new ArgumentException("Principal must not be finer than satang (2 decimal places).", nameof(principal));
        if (annualRate < 0)
            throw new ArgumentOutOfRangeException(nameof(annualRate), annualRate, "Annual rate cannot be negative.");
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), termMonths, "Term must be at least one month.");
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
