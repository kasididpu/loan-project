using LoanProject.Application.Reports;

namespace LoanProject.Infrastructure.ReadModel;

/// <summary>
/// Pure daily-collections math over installment rows — no database, so it is
/// unit-tested directly. For each UTC day in the window, the amount that fell
/// due (from the schedule) against the amount actually collected (from projected
/// payments). Days with no activity are omitted. Collection rate is a ratio, so
/// it rounds (AwayFromZero, 2 dp); it is null when nothing fell due that day.
/// </summary>
public static class DailyCollectionsMath
{
    public static IReadOnlyList<DailyCollectionRow> Compute(
        IReadOnlyCollection<InstallmentReadModel> installments, DateTime nowUtc, int windowDays)
    {
        var today = DateOnly.FromDateTime(nowUtc.Date);
        var fromDate = today.AddDays(-(windowDays - 1));

        bool InWindow(DateOnly day) => day >= fromDate && day <= today;

        var dueByDay = installments
            .Select(i => (Day: DateOnly.FromDateTime(i.DueDateUtc), i.DueAmount))
            .Where(x => InWindow(x.Day))
            .GroupBy(x => x.Day)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.DueAmount));

        var collectedByDay = installments
            .Where(i => i is { Paid: true, PaidAtUtc: not null, PaidAmount: not null })
            .Select(i => (Day: DateOnly.FromDateTime(i.PaidAtUtc!.Value), Amount: i.PaidAmount!.Value))
            .Where(x => InWindow(x.Day))
            .GroupBy(x => x.Day)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Amount));

        var rows = new List<DailyCollectionRow>();
        for (var date = fromDate; date <= today; date = date.AddDays(1))
        {
            var due = dueByDay.GetValueOrDefault(date);
            var collected = collectedByDay.GetValueOrDefault(date);
            if (due == 0m && collected == 0m)
                continue;

            var rate = due == 0m
                ? (decimal?)null
                : Math.Round(collected / due * 100m, 2, MidpointRounding.AwayFromZero);

            rows.Add(new DailyCollectionRow(date, due, collected, rate));
        }

        return rows;
    }
}
