using LoanProject.Application;
using LoanProject.Domain.Payments;
using LoanProject.Infrastructure.Persistence.Repositories;
using LoanProject.Infrastructure.Reports;

namespace LoanProject.Infrastructure.Tests;

public class EndOfDaySummaryQueryTests
{
    // Far outside the synthetic-volume range (Jan-Mar 2025) and the seed
    // data (2026), so this day belongs to summary tests alone.
    private static readonly DateOnly ReportDate = new(2030, 5, 5);

    [Fact]
    public async Task GetAsync_SumsOnlyThatDaysPaymentsPerLoan()
    {
        var loanId = Guid.NewGuid();
        var onThatDay1 = new Payment(
            Guid.NewGuid(), loanId, 8_884.88m, $"evt_eod_{Guid.NewGuid():N}",
            new DateTime(2030, 5, 5, 9, 0, 0, DateTimeKind.Utc));
        var onThatDay2 = new Payment(
            Guid.NewGuid(), loanId, 100.00m, $"evt_eod_{Guid.NewGuid():N}",
            new DateTime(2030, 5, 5, 17, 30, 0, DateTimeKind.Utc));
        var dayAfter = new Payment(
            Guid.NewGuid(), loanId, 500.00m, $"evt_eod_{Guid.NewGuid():N}",
            new DateTime(2030, 5, 6, 8, 0, 0, DateTimeKind.Utc));

        await using (var context = TestDatabase.CreateContext())
        {
            var repository = new PaymentRepository(context);
            repository.Add(onThatDay1);
            repository.Add(onThatDay2);
            repository.Add(dayAfter);
            await ((IUnitOfWork)context).SaveChangesAsync(CancellationToken.None);
        }

        var summaries = await new EndOfDaySummaryQuery(TestDatabase.ConnectionString)
            .GetAsync(ReportDate, CancellationToken.None);

        var row = Assert.Single(summaries, s => s.LoanId == loanId);
        Assert.Equal(2, row.PaymentsCount);                       // day-after payment excluded
        Assert.Equal(8_984.88m, row.TotalCollected);              // 8,884.88 + 100.00
        Assert.Equal(onThatDay2.PaidAtUtc, row.LastPaymentAtUtc); // the 17:30 one
    }
}
