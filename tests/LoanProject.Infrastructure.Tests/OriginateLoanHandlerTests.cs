using LoanProject.Application.Loans;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Rates;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Command side against the real event store. Confirms the rate is taken from
/// the sheet (not the caller) and a LoanOriginated event is appended.
/// </summary>
public class OriginateLoanHandlerTests
{
    [Fact]
    public async Task HandleAsync_LooksUpRate_AppendsOriginatedEvent()
    {
        var rates = new StaticRateSheet();
        var repository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var handler = new OriginateLoanHandler(repository, rates);
        var customerId = Guid.NewGuid();

        var loanId = await handler.HandleAsync(customerId, 80_000m, RateType.Effective, 12, CancellationToken.None);
        var expectedRate = await rates.GetAnnualRateAsync(RateType.Effective, 12, CancellationToken.None);

        var loan = await repository.LoadAsync(loanId, CancellationToken.None);
        Assert.NotNull(loan);
        Assert.Equal(LoanStatus.Originated, loan!.Status);
        Assert.Equal(80_000m, loan.Principal);
        Assert.Equal(expectedRate, loan.AnnualRate); // from the sheet, not supplied by the caller
    }
}
