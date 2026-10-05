using IskBattle.Domain.Periods;

namespace IskBattle.Domain.CheckIns;

/// <summary>A check-in window in Stockholm local time: open from <see cref="Opens"/> (inclusive) to <see cref="Closes"/> (exclusive).</summary>
public sealed record CheckInWindow(DateOnly TradingDay, DateTime Opens, DateTime Closes);

/// <summary>
/// When a check-in may be submitted and which trading day it reports on (GAME-DESIGN.md §4a). All times are
/// Europe/Stockholm local.
/// <list type="bullet">
/// <item>Daily: every trading day, 22:30 until 09:00 the next day.</item>
/// <item>Weekly: Fridays, 22:30 until Sunday 23:59.</item>
/// <item>Monthly: the month's last trading day, 22:30 until 09:00 on the next trading day.</item>
/// <item>Season end: the year's last trading day has a window for every cadence, and no window reporting on
/// a December day stays open past midnight on New Year's Eve (brokers reset "i år" at midnight).</item>
/// </list>
/// Every window opens at 22:30 on its trading day and closes before the next trading day's 22:30, so at any
/// moment all open windows report on the same trading day. A player may use the union of the windows of
/// their active leagues' cadences.
/// </summary>
public static class CheckInWindows
{
    /// <summary>The window for <paramref name="cadence"/> reporting on <paramref name="tradingDay"/>, or null if there is none.</summary>
    public static CheckInWindow? For(DateOnly tradingDay, Cadence cadence)
    {
        if (!PeriodCalculator.IsTradingDay(tradingDay))
            return null;

        var opens = tradingDay.ToDateTime(GameConfig.CheckInWindowOpens);
        DateTime? closes = IsSeasonEnd(tradingDay) ? NewYear(tradingDay) : cadence switch
        {
            Cadence.Daily => tradingDay.AddDays(1).ToDateTime(GameConfig.CheckInWindowCloses),
            Cadence.Weekly when tradingDay.DayOfWeek == DayOfWeek.Friday => tradingDay.AddDays(3).ToDateTime(TimeOnly.MinValue),
            Cadence.Monthly when IsLastTradingDayOfMonth(tradingDay) => NextTradingDay(tradingDay).ToDateTime(GameConfig.CheckInWindowCloses),
            Cadence.Weekly or Cadence.Monthly => null,
            _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null),
        };
        if (closes is null)
            return null;
        // Nothing reporting on the season stays open into the next one.
        var close = closes.Value < NewYear(tradingDay) ? closes.Value : NewYear(tradingDay);
        return new CheckInWindow(tradingDay, opens, close);
    }

    /// <summary>The open window (longest of the cadences' windows) at <paramref name="now"/>, or null if none is open.</summary>
    /// <param name="cadencesForSeason">The cadences of the player's active leagues in a season. None means Daily
    /// (a new or only Pending player can still check in).</param>
    public static CheckInWindow? Open(DateTime now, Func<int, IReadOnlyCollection<Cadence>> cadencesForSeason)
    {
        var day = LatestOpenedTradingDay(now);
        return WindowsFor(day, cadencesForSeason)
            .Where(w => now >= w.Opens && now < w.Closes)
            .MaxBy(w => w.Closes);
    }

    /// <summary>The first window opening after <paramref name="now"/>.</summary>
    public static CheckInWindow Next(DateTime now, Func<int, IReadOnlyCollection<Cadence>> cadencesForSeason)
    {
        for (var day = DateOnly.FromDateTime(now); ; day = day.AddDays(1))
        {
            var next = WindowsFor(day, cadencesForSeason).Where(w => w.Opens > now).MaxBy(w => w.Closes);
            if (next is not null)
                return next;
        }
    }

    /// <summary>The latest trading day whose window opening time has passed.</summary>
    public static DateOnly LatestOpenedTradingDay(DateTime now)
    {
        var day = DateOnly.FromDateTime(now);
        if (TimeOnly.FromDateTime(now) < GameConfig.CheckInWindowOpens)
            day = day.AddDays(-1);
        while (!PeriodCalculator.IsTradingDay(day))
            day = day.AddDays(-1);
        return day;
    }

    /// <summary>The year's last trading day, whose window is the season's final one for every cadence.</summary>
    public static bool IsSeasonEnd(DateOnly tradingDay) =>
        PeriodCalculator.IsTradingDay(tradingDay) && NextTradingDay(tradingDay).Year != tradingDay.Year;

    private static IEnumerable<CheckInWindow> WindowsFor(DateOnly day, Func<int, IReadOnlyCollection<Cadence>> cadencesForSeason)
    {
        var cadences = cadencesForSeason(day.Year);
        return (cadences.Count == 0 ? [Cadence.Daily] : cadences).Select(c => For(day, c)).OfType<CheckInWindow>();
    }

    private static bool IsLastTradingDayOfMonth(DateOnly day) => NextTradingDay(day).Month != day.Month;

    private static DateOnly NextTradingDay(DateOnly day)
    {
        do day = day.AddDays(1);
        while (!PeriodCalculator.IsTradingDay(day));
        return day;
    }

    private static DateTime NewYear(DateOnly day) => new(day.Year + 1, 1, 1);
}
