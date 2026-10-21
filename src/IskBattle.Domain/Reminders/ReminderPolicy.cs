using System.Globalization;
using IskBattle.Domain.CheckIns;

namespace IskBattle.Domain.Reminders;

/// <param name="WindowKey">Identifies the check-in window by cadence and trading day, e.g. "D:2026-09-21",
/// "W:2026-09-25", "M:2026-09-30".</param>
public sealed record ReminderDecision(string WindowKey, bool IsDue);

/// <summary>
/// When to remind a player to check in (GAME-DESIGN.md §4a), at most once per window. The reminder goes out at
/// 08:00 the day after the window's trading day (inside the open window), if there is no check-in for that
/// trading day: Daily after every trading day, Weekly after the week's last trading day (usually on Saturday),
/// Monthly after the month's last trading day.
/// The season's final window closes at midnight, so its reminder goes out at 20:00 on the last trading day.
/// <para>
/// The reminder job runs on a schedule (ARCHITECTURE.md §8), not by polling. <see cref="Evaluate"/> still treats a
/// reminder as due until its window closes, so a retried or later run inside the window sends it.
/// </para>
/// </summary>
public static class ReminderPolicy
{
    /// <summary>
    /// Whether a scheduled run at <paramref name="stockholmNow"/> could have any reminder to send: within
    /// <see cref="GameConfig.ReminderRunSpan"/> of 08:00, or of 20:00 on the year's last trading day. Needs no
    /// data, so the job can exit before touching the database (which then stays auto-paused).
    /// </summary>
    public static bool IsRunTime(DateTimeOffset stockholmNow)
    {
        var now = stockholmNow.DateTime;
        var today = DateOnly.FromDateTime(now);
        return Within(GameConfig.ReminderTime)
               || (CheckInWindows.IsSeasonEnd(today) && Within(GameConfig.SeasonEndReminderTime));

        bool Within(TimeOnly start) => now >= today.ToDateTime(start) && now < today.ToDateTime(start) + GameConfig.ReminderRunSpan;
    }

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
