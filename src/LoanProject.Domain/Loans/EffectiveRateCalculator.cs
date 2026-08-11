namespace LoanProject.Domain.Loans;

/// <summary>
/// Recovers the effective interest rate (IRR) implied by an annuity cash
/// flow: the monthly rate at which the present value of all payments equals
/// the principal. There is no closed-form solution — the rate appears both
/// as an exponent and a divisor — so the root is found by bisection, chosen
/// over Newton-Raphson because it is guaranteed to converge and trivial to
/// explain; speed is irrelevant at this scale.
///
/// Rates are NOT money: they are returned at full precision (tolerance
/// 1e-9), never rounded to satang. Callers round for display only.
/// The annual convention in this project is nominal (monthly x 12), matching
/// how annual rates are divided by 12 on the way in.
/// </summary>
public static class EffectiveRateCalculator
{
    private const decimal Tolerance = 0.000000001m; // 1e-9 on the monthly rate

    public static decimal MonthlyRateFromAnnuity(decimal principal, decimal payment, int termMonths)
    {
        if (principal <= 0)
            throw new ArgumentOutOfRangeException(nameof(principal), principal, "Principal must be positive.");
        if (payment <= 0)
            throw new ArgumentOutOfRangeException(nameof(payment), payment, "Payment must be positive.");
        if (termMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(termMonths), termMonths, "Term must be at least one month.");

        var totalRepaid = payment * termMonths;
        if (totalRepaid < principal)
            throw new ArgumentException(
                "Total repayments are below the principal, which implies a negative rate.", nameof(payment));
        if (totalRepaid == principal)
            return 0m;

        // PV of the annuity is strictly decreasing in the rate, so the root
        // is bracketed between "almost zero" and 100% per month; each pass
        // halves the interval until it is narrower than the tolerance.
        var lo = Tolerance;
        var hi = 1m;
        while (hi - lo > Tolerance)
        {
            var mid = (lo + hi) / 2m;
            if (PresentValueExceedsPrincipal(principal, payment, termMonths, mid))
                lo = mid; // discounting too weak -> the true rate is higher
            else
                hi = mid;
        }

        return (lo + hi) / 2m;
    }

    /// <summary>Headline conversion: what a quoted flat rate really costs per year (nominal, x12).</summary>
    public static decimal AnnualEffectiveFromFlat(decimal principal, decimal annualFlatRate, int termMonths)
    {
        var payment = AmortizationCalculator.BuildFlatSchedule(principal, annualFlatRate, termMonths)[0].Payment;
        return MonthlyRateFromAnnuity(principal, payment, termMonths) * 12m;
    }

    private static bool PresentValueExceedsPrincipal(decimal principal, decimal payment, int termMonths, decimal monthlyRate)
    {
        var compound = 1m;
        for (var month = 0; month < termMonths; month++)
        {
            compound *= 1m + monthlyRate;
            // Past this point 1/compound cannot move the PV at our tolerance,
            // and decimal would overflow on long terms at high trial rates.
            if (compound > 1_000_000_000_000_000m)
                break;
        }

        var presentValue = payment * (1m - 1m / compound) / monthlyRate;
        return presentValue > principal;
    }
}
