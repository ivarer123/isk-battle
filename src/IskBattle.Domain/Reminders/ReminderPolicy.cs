using System.Globalization;
using IskBattle.Domain.Periods;

namespace IskBattle.Domain.Reminders;

/// <param name="WindowKey">Identifies the check-in window, e.g. "D:2026-09-21", "W:2026-38", "M:2026-09".</param>
public sealed record ReminderDecision(string WindowKey, bool IsDue);

/// <summary>
/// When to remind a player to check in (GAME-DESIGN.md §9), at most once per window:
/// <list type="bullet">
/// <item>Daily: trading days from 18:00, if there is no check-in today.</item>
/// <item>Weekly: from Friday 18:00 until the week ends, if there is no check-in since Monday.</item>
/// <item>Monthly: from 18:00 on the last trading day of the month until it ends, if there is no check-in this month.</item>
/// </list>
/// A reminder that was missed at 18:00 (e.g. the job was down) is still sent later in the same window.
/// </summary>
public static class ReminderPolicy
{
    public static ReminderDecision Evaluate(Cadence cadence, DateTimeOffset stockholmNow, DateOnly? latestCheckIn)
    {
        var today = DateOnly.FromDateTime(stockholmNow.DateTime);
        var afterReminderTime = TimeOnly.FromDateTime(stockholmNow.DateTime) >= GameConfig.ReminderTime;

        var (key, windowStart, reminderDay) = cadence switch
        {
            Cadence.Daily => ($"D:{today:yyyy-MM-dd}", today, today),
            Cadence.Weekly => WeekWindow(today),
            Cadence.Monthly => MonthWindow(today),
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null),
        };

        var reminderDayReached = today > reminderDay || (today == reminderDay && afterReminderTime);
        var due = PeriodCalculator.IsTradingDay(reminderDay)
                  && reminderDayReached
                  && !(latestCheckIn >= windowStart);
        return new ReminderDecision(key, due);
    }

    private static (string, DateOnly, DateOnly) WeekWindow(DateOnly today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var date = today.ToDateTime(TimeOnly.MinValue);
        var key = string.Create(CultureInfo.InvariantCulture, $"W:{ISOWeek.GetYear(date)}-{ISOWeek.GetWeekOfYear(date):00}");
        return (key, monday, monday.AddDays(4));
    }

    private static (string, DateOnly, DateOnly) MonthWindow(DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        while (!PeriodCalculator.IsTradingDay(last))
            last = last.AddDays(-1);
        return ($"M:{today:yyyy-MM}", first, last);
    }
}
