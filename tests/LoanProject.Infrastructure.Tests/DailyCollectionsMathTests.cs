using LoanProject.Infrastructure.ReadModel;

namespace LoanProject.Infrastructure.Tests;

/// <summary>
/// Pure daily-collections math — no database. Due comes from the schedule, the
/// collected side from projected payments, grouped by UTC calendar day.
/// </summary>
public class DailyCollectionsMathTests
{
    private static readonly DateTime Now = new(2026, 8, 15, 12, 0, 0, DateTimeKind.Utc);
    private const int Window = 30;

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static InstallmentReadModel Due(DateTime dueUtc, decimal dueAmount) => new()
    {
        LoanId = Guid.NewGuid(),
        InstallmentNo = 1,
        DueDateUtc = dueUtc,
        DueAmount = dueAmount,
        Paid = false,
    };

    private static InstallmentReadModel DuePaid(DateTime dueUtc, decimal dueAmount, DateTime paidUtc, decimal paidAmount) => new()
    {
        LoanId = Guid.NewGuid(),
        InstallmentNo = 1,
        DueDateUtc = dueUtc,
        DueAmount = dueAmount,
        Paid = true,
        PaidAtUtc = paidUtc,
        PaidAmount = paidAmount,
    };

    [Fact]
    public void Compute_DueAndCollectedSameDay_RateHundred()
    {
        var rows = DailyCollectionsMath.Compute(
            new[] { DuePaid(Utc(2026, 8, 10), 5_000m, Utc(2026, 8, 10), 5_000m) }, Now, Window);

        var row = Assert.Single(rows);
        Assert.Equal(new DateOnly(2026, 8, 10), row.Date);
        Assert.Equal(5_000m, row.Due);
        Assert.Equal(5_000m, row.Collected);
        Assert.Equal(100.00m, row.CollectionRatePercent);
    }

    [Fact]
    public void Compute_DueUnpaid_RateZero()
    {
        var rows = DailyCollectionsMath.Compute(new[] { Due(Utc(2026, 8, 10), 5_000m) }, Now, Window);

        var row = Assert.Single(rows);
        Assert.Equal(5_000m, row.Due);
        Assert.Equal(0m, row.Collected);
        Assert.Equal(0.00m, row.CollectionRatePercent);
    }

    [Fact]
    public void Compute_PaidOnDifferentDayThanDue_SplitsIntoTwoRows()
    {
        var rows = DailyCollectionsMath.Compute(
            new[] { DuePaid(Utc(2026, 8, 5), 5_000m, Utc(2026, 8, 12), 5_000m) }, Now, Window);

        Assert.Equal(2, rows.Count);

        var dueDay = rows.Single(r => r.Date == new DateOnly(2026, 8, 5));
        Assert.Equal(5_000m, dueDay.Due);
        Assert.Equal(0m, dueDay.Collected);
        Assert.Equal(0.00m, dueDay.CollectionRatePercent);

        var paidDay = rows.Single(r => r.Date == new DateOnly(2026, 8, 12));
        Assert.Equal(0m, paidDay.Due);
        Assert.Equal(5_000m, paidDay.Collected);
        Assert.Null(paidDay.CollectionRatePercent); // nothing was due -> no denominator
    }

    [Fact]
    public void Compute_OutsideWindow_Excluded()
    {
        var rows = DailyCollectionsMath.Compute(new[] { Due(Utc(2026, 1, 1), 5_000m) }, Now, Window);

        Assert.Empty(rows);
    }

    [Fact]
    public void Compute_RoundsRateAwayFromZero()
    {
        var rows = DailyCollectionsMath.Compute(
            new[] { DuePaid(Utc(2026, 8, 10), 300m, Utc(2026, 8, 10), 100m) }, Now, Window);

        // 100 / 300 = 33.333... -> 33.33
        Assert.Equal(33.33m, Assert.Single(rows).CollectionRatePercent);
    }
}
