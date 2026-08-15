using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.ReadModel;

namespace LoanProject.Infrastructure.Tests;

/// <summary>Query side against the real Read DB, populated through the projection.</summary>
public class LoanStatusQueryTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetAsync_AfterProjection_ReturnsView()
    {
        var loanId = Guid.NewGuid();
        var loan = Loan.Originate(loanId, Guid.NewGuid(), 90_000m, 0.16m, RateType.Effective, 12, Now);
        loan.Approve("officer", Now);
        await ReadModelTesting.ProjectAllAsync(ReadModelTesting.Envelopes(loan));

        await using var db = TestReadDatabase.CreateContext();
        var view = await new LoanStatusQuery(db).GetAsync(loanId, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal("Approved", view!.Status);
        Assert.Equal(90_000m, view.Principal);
        Assert.Equal(2, view.Version); // originated + approved
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        await using var db = TestReadDatabase.CreateContext();

        var view = await new LoanStatusQuery(db).GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(view);
    }
}
