using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.Rates;

namespace LoanProject.Infrastructure.Tests;

public class StaticRateSheetTests
{
    private static readonly StaticRateSheet Sheet = new();

    [Theory]
    [InlineData(6, 0.12)]
    [InlineData(60, 0.12)]
    public async Task GetAnnualRateAsync_Flat_OneRateRegardlessOfTerm(int termMonths, decimal expected)
    {
        Assert.Equal(expected,
            await Sheet.GetAnnualRateAsync(RateType.Flat, termMonths, CancellationToken.None));
    }

    [Theory]
    [InlineData(12, 0.16)] // last month of the first tier
    [InlineData(13, 0.18)] // first month of the second
    [InlineData(36, 0.18)]
    [InlineData(37, 0.20)]
    public async Task GetAnnualRateAsync_Effective_TiersByTermBoundary(int termMonths, decimal expected)
    {
        Assert.Equal(expected,
            await Sheet.GetAnnualRateAsync(RateType.Effective, termMonths, CancellationToken.None));
    }

    [Fact]
    public async Task GetAnnualRateAsync_NonPositiveTerm_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Sheet.GetAnnualRateAsync(RateType.Flat, 0, CancellationToken.None));
    }
}
