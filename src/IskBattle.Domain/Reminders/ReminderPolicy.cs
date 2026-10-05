using System.Globalization;
using IskBattle.Domain.Periods;

namespace IskBattle.Domain.Reminders;

/// <param name="WindowKey">Identifies the check-in window, e.g. "D:2026-09-21", "W:2026-09-14" (first day of the
/// week within the season), "M:2026-09".</param>
public sealed record ReminderDecision(string WindowKey, bool IsDue);

/// <summary>
/// When to remind a player to check in (GAME-DESIGN.md §9), at most once per window:
/// <list type="bullet">
/// <item>Daily: trading days from 18:00, if there is no check-in today.</item>
/// <item>Weekly: from Friday 18:00 until the week ends, if there is no check-in since Monday. A week split at New
/// Year (e.g. 2026-W53) is two windows: the old season's part is reminded on its last trading day, the new
/// season's part on its last trading day up to Friday, and only check-ins within the part count.</item>
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
            Cadence.Daily => ($"D:{today:yyyy-MM-dd}", today, (DateOnly?)today),
            Cadence.Weekly => WeekWindow(today),
            Cadence.Monthly => MonthWindow(today),
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null),
        };

        var reminderDayReached = today > reminderDay || (today == reminderDay && afterReminderTime);
        var due = reminderDay is not null
                  && PeriodCalculator.IsTradingDay(reminderDay.Value)
                  && reminderDayReached
                  && !(latestCheckIn >= windowStart);
        return new ReminderDecision(key, due);
    }

    /// <returns>The reminder day is null when the season's part of the week has no trading day up to Friday.</returns>
    private static (string, DateOnly, DateOnly?) WeekWindow(DateOnly today)
    {
        var week = PeriodCalculator.For(PeriodType.Week, today); // clipped to the season
        var friday = week.Start.AddDays(4 - (((int)week.Start.DayOfWeek + 6) % 7));
        DateOnly? reminderDay = week.End.AddDays(-1) < friday ? week.End.AddDays(-1) : friday;
        while (reminderDay >= week.Start && !PeriodCalculator.IsTradingDay(reminderDay.Value))
            reminderDay = reminderDay.Value.AddDays(-1);
        if (reminderDay < week.Start)
            reminderDay = null;
        return (string.Create(CultureInfo.InvariantCulture, $"W:{week.Start:yyyy-MM-dd}"), week.Start, reminderDay);
    }

    private static (string, DateOnly, DateOnly?) MonthWindow(DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        while (!PeriodCalculator.IsTradingDay(last))
            last = last.AddDays(-1);
        return ($"M:{today:yyyy-MM}", first, last);
    }
}
