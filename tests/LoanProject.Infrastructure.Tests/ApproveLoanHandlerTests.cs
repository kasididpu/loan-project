using LoanProject.Application.Customers;
using LoanProject.Application.Loans;
using LoanProject.Domain.Customers;
using LoanProject.Domain.Loans;
using LoanProject.Infrastructure.EventStore;
using LoanProject.Infrastructure.Persistence.Repositories;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// The Phase 7 KYC gate against the real write DB: approval is allowed only for
/// a verified customer, otherwise it is rejected and the loan stays Originated.
/// </summary>
public class ApproveLoanHandlerTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 10, 0, 0, DateTimeKind.Utc);

    private static async Task<Guid> SeedCustomerAsync(KycStatus status)
    {
        var id = Guid.NewGuid();
        await using var db = TestDatabase.CreateContext();
        var customer = new Customer(id, "KYC Test", Now);
        customer.SetKycStatus(status);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Guid> OriginateLoanForAsync(Guid customerId)
    {
        var repository = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        var loan = Loan.Originate(Guid.NewGuid(), customerId, 100_000m, 0.12m, RateType.Effective, 12, Now);
        await repository.SaveAsync(loan, CancellationToken.None);
        return loan.Id;
    }

    [Fact]
    public async Task HandleAsync_CustomerVerified_ApprovesLoan()
    {
        var customerId = await SeedCustomerAsync(KycStatus.Verified);
        var loanId = await OriginateLoanForAsync(customerId);
        var loans = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        await using var db = TestDatabase.CreateContext();

        await new ApproveLoanHandler(loans, new CustomerRepository(db))
            .HandleAsync(loanId, "officer", CancellationToken.None);

        var reloaded = await loans.LoadAsync(loanId, CancellationToken.None);
        Assert.Equal(LoanStatus.Approved, reloaded!.Status);
    }

    [Theory]
    [InlineData(KycStatus.Pending)]
    [InlineData(KycStatus.Rejected)]
    public async Task HandleAsync_CustomerNotVerified_ThrowsAndLeavesLoanOriginated(KycStatus status)
    {
        var customerId = await SeedCustomerAsync(status);
        var loanId = await OriginateLoanForAsync(customerId);
        var loans = new LoanEventStoreRepository(TestDatabase.ConnectionString);
        await using var db = TestDatabase.CreateContext();
        var handler = new ApproveLoanHandler(loans, new CustomerRepository(db));

        await Assert.ThrowsAsync<KycNotVerifiedException>(
            () => handler.HandleAsync(loanId, "officer", CancellationToken.None));

        var reloaded = await loans.LoadAsync(loanId, CancellationToken.None);
        Assert.Equal(LoanStatus.Originated, reloaded!.Status); // gate blocked the approval
    }
}
