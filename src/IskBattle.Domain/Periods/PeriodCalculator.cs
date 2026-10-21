namespace IskBattle.Domain.Periods;

/// <summary>
/// Maps a Stockholm date to its standings period (GAME-DESIGN.md §6).
/// <para>
/// Every period is clipped to its season (calendar year), so the Dag, Vecka and Månad periods each
/// partition the season without gaps. An ISO week spanning New Year (e.g. 2026-W53) is split in two.
/// </para>
/// <para>
/// Dag runs from a trading day to the next trading day, so weekend and holiday check-ins belong to the
/// preceding trading day. The season's first Dag starts on 1 January, even when 1 January is not a trading day.
/// </para>
/// </summary>
public static class PeriodCalculator
{
    /// <summary>A weekday on which Nasdaq Stockholm is open (<see cref="ExchangeHolidays"/>).</summary>
    public static bool IsTradingDay(DateOnly date) =>
        GameConfig.TradingDays.Contains(date.DayOfWeek) && !ExchangeHolidays.IsHoliday(date);

    public static Period For(PeriodType type, DateOnly date) => type switch
    {
        PeriodType.Day => Day(date),
        PeriodType.Week => Week(date),
        PeriodType.Month => Month(date),
        PeriodType.Year => Year(date),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static Period Day(DateOnly date)
    {
        var (seasonStart, seasonEnd) = Season(date);
        var firstTradingDay = NextTradingDayOnOrAfter(seasonStart, seasonEnd);

        var tradingDay = date;
        while (tradingDay > firstTradingDay && !IsTradingDay(tradingDay))
            tradingDay = tradingDay.AddDays(-1);
        if (tradingDay < firstTradingDay)
            tradingDay = firstTradingDay;

        var start = tradingDay == firstTradingDay ? seasonStart : tradingDay;
        var end = NextTradingDayOnOrAfter(tradingDay.AddDays(1), seasonEnd);
        return new Period(PeriodType.Day, start, end);
    }

    private static Period Week(DateOnly date)
    {
        var (seasonStart, seasonEnd) = Season(date);
        var monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var start = monday < seasonStart ? seasonStart : monday;
        var end = monday.AddDays(7) > seasonEnd ? seasonEnd : monday.AddDays(7);
        return new Period(PeriodType.Week, start, end);
    }

    private static Period Month(DateOnly date)
    {
        var start = new DateOnly(date.Year, date.Month, 1);
        return new Period(PeriodType.Month, start, start.AddMonths(1));
    }

    private static Period Year(DateOnly date)
    {
        var (seasonStart, seasonEnd) = Season(date);
        return new Period(PeriodType.Year, seasonStart, seasonEnd);
    }

    private static (DateOnly Start, DateOnly End) Season(DateOnly date)
    {
        var start = new DateOnly(date.Year, 1, 1);
        return (start, start.AddYears(1));
    }

    /// <summary>First trading day on or after <paramref name="date"/>, or <paramref name="limit"/> if none comes first.</summary>
    private static DateOnly NextTradingDayOnOrAfter(DateOnly date, DateOnly limit)
    {
        while (date < limit && !IsTradingDay(date))
            date = date.AddDays(1);
        return date;
    }
}
