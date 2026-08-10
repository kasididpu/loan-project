namespace LoanProject.Domain.Loans;

/// <summary>One row of an amortization schedule. All money is satang-precise (2 dp).</summary>
public sealed record Installment(
    int Number,
    decimal Payment,
    decimal InterestPortion,
    decimal PrincipalPortion,
    decimal RemainingBalance);
