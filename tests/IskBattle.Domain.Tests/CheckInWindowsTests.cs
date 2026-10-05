using IskBattle.Domain.CheckIns;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class CheckInWindowsTests
{
    private static DateTime T(int y, int m, int d, int hour, int minute = 0) => new(y, m, d, hour, minute, 0);

    private static Func<int, IReadOnlyCollection<Cadence>> Cadences(params Cadence[] cadences) => _ => cadences;

    [Fact]
    public void Daily_window_runs_from_2230_to_0900_the_next_day()
    {
        Assert.Equal(new CheckInWindow(D(2026, 9, 21), T(2026, 9, 21, 22, 30), T(2026, 9, 22, 9)), CheckInWindows.For(D(2026, 9, 21), Cadence.Daily));
        Assert.Equal(T(2026, 9, 26, 9), CheckInWindows.For(D(2026, 9, 25), Cadence.Daily)!.Closes); // Friday → Saturday 09:00
    }

    [Fact]
    public void Weekly_window_is_friday_2230_to_sunday_midnight()
    {
        Assert.Equal(new CheckInWindow(D(2026, 9, 25), T(2026, 9, 25, 22, 30), T(2026, 9, 28, 0)), CheckInWindows.For(D(2026, 9, 25), Cadence.Weekly));
        Assert.Null(CheckInWindows.For(D(2026, 9, 24), Cadence.Weekly));
    }

    [Fact]
    public void Monthly_window_closes_before_the_next_trading_day_opens()
    {
        // Wed 30 Sep → Thu 1 Oct 09:00; Fri 30 Oct → Mon 2 Nov 09:00.
        Assert.Equal(T(2026, 10, 1, 9), CheckInWindows.For(D(2026, 9, 30), Cadence.Monthly)!.Closes);
        Assert.Equal(T(2026, 11, 2, 9), CheckInWindows.For(D(2026, 10, 30), Cadence.Monthly)!.Closes);
        Assert.Null(CheckInWindows.For(D(2026, 9, 29), Cadence.Monthly));
    }

    [Fact]
    public void No_windows_on_weekends() =>
        Assert.All(Enum.GetValues<Cadence>(), c => Assert.Null(CheckInWindows.For(D(2026, 9, 26), c)));

    [Theory]
    [InlineData(Cadence.Daily)]
    [InlineData(Cadence.Weekly)]
    [InlineData(Cadence.Monthly)]
    public void Season_end_has_a_window_for_every_cadence_closing_at_midnight(Cadence cadence)
    {
        // Thursday 31 Dec 2026 is the last trading day; brokers reset "i år" at midnight.
        Assert.Equal(new CheckInWindow(D(2026, 12, 31), T(2026, 12, 31, 22, 30), T(2027, 1, 1, 0)), CheckInWindows.For(D(2026, 12, 31), cadence));
        // 2022 ended on a Saturday: the last trading day is Friday 30 Dec, still closing at New Year.
        Assert.Equal(T(2023, 1, 1, 0), CheckInWindows.For(D(2022, 12, 30), cadence)!.Closes);
    }

    [Fact]
    public void Day_before_season_end_is_not_a_season_end() =>
        Assert.Equal((false, true), (CheckInWindows.IsSeasonEnd(D(2026, 12, 30)), CheckInWindows.IsSeasonEnd(D(2026, 12, 31))));

    [Fact]
    public void Unknown_cadence_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CheckInWindows.For(D(2026, 9, 21), (Cadence)9));

    [Theory]
    [InlineData(2026, 9, 21, 22, 29, null)]        // Monday, not yet open
    [InlineData(2026, 9, 21, 22, 30, 21)]
    [InlineData(2026, 9, 22, 8, 59, 21)]
    [InlineData(2026, 9, 22, 9, 0, null)]
    [InlineData(2026, 9, 26, 10, 0, null)]         // Saturday after the Daily window closed
    public void Open_daily_window(int y, int m, int d, int hour, int minute, int? tradingDay) =>
        Assert.Equal(tradingDay, CheckInWindows.Open(T(y, m, d, hour, minute), Cadences(Cadence.Daily))?.TradingDay.Day);

    [Fact]
    public void Open_uses_the_union_of_cadences_and_the_longest_window()
    {
        var saturday = T(2026, 9, 26, 15);

        Assert.Null(CheckInWindows.Open(saturday, Cadences(Cadence.Daily)));
        Assert.Equal(T(2026, 9, 28, 0), CheckInWindows.Open(saturday, Cadences(Cadence.Daily, Cadence.Weekly))!.Closes);
        Assert.Equal(T(2026, 9, 26, 9), CheckInWindows.Open(T(2026, 9, 26, 8), Cadences(Cadence.Daily))!.Closes);
    }

    [Fact]
    public void No_active_league_means_daily_windows() =>
        Assert.Equal(D(2026, 9, 21), CheckInWindows.Open(T(2026, 9, 21, 23), Cadences())!.TradingDay);

    [Fact]
    public void Next_window_follows_the_players_cadences()
    {
        var tuesdayNoon = T(2026, 9, 22, 12);

        Assert.Equal(T(2026, 9, 22, 22, 30), CheckInWindows.Next(tuesdayNoon, Cadences(Cadence.Daily)).Opens);
        Assert.Equal(T(2026, 9, 25, 22, 30), CheckInWindows.Next(tuesdayNoon, Cadences(Cadence.Weekly)).Opens);
        Assert.Equal(T(2026, 9, 30, 22, 30), CheckInWindows.Next(tuesdayNoon, Cadences(Cadence.Monthly)).Opens);
        // Inside the Daily window, the next one is tomorrow's.
        Assert.Equal(T(2026, 9, 23, 22, 30), CheckInWindows.Next(T(2026, 9, 22, 23), Cadences(Cadence.Daily)).Opens);
    }

    [Fact]
    public void Weekly_player_gets_the_season_end_window_before_the_new_years_friday() =>
        Assert.Equal(
            [D(2026, 12, 25), D(2026, 12, 31), D(2027, 1, 1)],
            new[] { T(2026, 12, 21, 12), T(2026, 12, 26, 12), T(2027, 1, 1, 0) }
                .Select(t => CheckInWindows.Next(t, Cadences(Cadence.Weekly)).TradingDay));

    [Fact]
    public void Cadence_is_looked_up_for_the_season_of_the_window()
    {
        // Weekly in 2026, Daily from 2027.
        Func<int, IReadOnlyCollection<Cadence>> cadences = season => season == 2026 ? [Cadence.Weekly] : [Cadence.Daily];

        Assert.Equal(D(2026, 12, 31), CheckInWindows.Next(T(2026, 12, 28, 12), cadences).TradingDay);
        Assert.Equal(D(2027, 1, 4), CheckInWindows.Next(T(2027, 1, 2, 0), cadences).TradingDay);
    }

    [Theory]
    [InlineData(2026, 9, 28, 10, 0, 25)] // Monday morning → Friday
    [InlineData(2026, 9, 28, 22, 30, 28)]
    public void Latest_opened_trading_day(int y, int m, int d, int hour, int minute, int day) =>
        Assert.Equal(day, CheckInWindows.LatestOpenedTradingDay(T(y, m, d, hour, minute)).Day);
}
