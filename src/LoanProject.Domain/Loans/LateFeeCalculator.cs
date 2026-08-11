namespace LoanProject.Domain.Loans;

/// <summary>
/// Late-payment fee, pro-rata by actual days overdue.
///
/// Basis: the PRINCIPAL PORTION of the overdue installment only — per Bank
/// of Thailand guidance (2020): installments not yet due must not be
/// penalised, and the fee accrues on principal, not on interest, to avoid
/// interest-on-interest. Day-count convention: actual/365, fixed (leap years
/// ignored). Fees are money: rounded to satang, AwayFromZero.
///
/// Deliberately NOT wired into the Loan aggregate yet: applying a fee needs
/// "due date vs actual payment date", which arrives with the payment flow.
/// </summary>
public static class LateFeeCalculator
{
    public static decimal Calculate(decimal overduePrincipal, decimal annualPenaltyRate, int daysLate)
    {
        if (overduePrincipal <= 0)
            throw new ArgumentOutOfRangeException(nameof(overduePrincipal), overduePrincipal, "Overdue principal must be positive.");
        if (annualPenaltyRate < 0)
            throw new ArgumentOutOfRangeException(nameof(annualPenaltyRate), annualPenaltyRate, "Penalty rate cannot be negative.");
        if (daysLate < 0)
            throw new ArgumentOutOfRangeException(nameof(daysLate), daysLate, "Days late cannot be negative.");

        return Math.Round(overduePrincipal * annualPenaltyRate * daysLate / 365m, 2, MidpointRounding.AwayFromZero);
    }
}
