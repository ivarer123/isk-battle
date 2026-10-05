using System.Globalization;
using IskBattle.Domain.CheckIns;

namespace IskBattle.Domain.Reminders;

/// <param name="WindowKey">Identifies the check-in window by cadence and trading day, e.g. "D:2026-09-21",
/// "W:2026-09-25", "M:2026-09-30".</param>
public sealed record ReminderDecision(string WindowKey, bool IsDue);

/// <summary>
/// When to remind a player to check in (GAME-DESIGN.md §4a), at most once per window. The reminder goes out at
/// 08:00 the day after the window's trading day (inside the open window), if there is no check-in for that
/// trading day: Daily Tue–Sat, Weekly on Saturday, Monthly the day after the month's last trading day.
/// The season's final window closes at midnight, so its reminder goes out at 20:00 on the last trading day.
/// A reminder that was missed (e.g. the job was down) is still sent later while the window is open.
/// </summary>
public static class ReminderPolicy
{
    public static ReminderDecision Evaluate(Cadence cadence, DateTimeOffset stockholmNow, DateOnly? latestCheckIn)
    {
        var now = stockholmNow.DateTime;
        var today = DateOnly.FromDateTime(now);
        var (day, remindAt) = CheckInWindows.IsSeasonEnd(today) && TimeOnly.FromDateTime(now) >= GameConfig.SeasonEndReminderTime
            ? (today, today.ToDateTime(GameConfig.SeasonEndReminderTime))
            : (CheckInWindows.LatestOpenedTradingDay(now), CheckInWindows.LatestOpenedTradingDay(now).AddDays(1).ToDateTime(GameConfig.ReminderTime));

        var window = CheckInWindows.For(day, cadence);
        var key = string.Create(CultureInfo.InvariantCulture, $"{cadence.ToString()[0]}:{day:yyyy-MM-dd}");
        var due = window is not null
                  && now >= remindAt
                  && now < window.Closes
                  && !(latestCheckIn >= day);
        return new ReminderDecision(key, due);
    }
}
