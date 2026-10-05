using IskBattle.Domain.Reminders;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class ReminderPolicyTests
{
    private static DateTimeOffset At(int y, int m, int d, int hour, int minute = 0) =>
        new(y, m, d, hour, minute, 0, TimeSpan.FromHours(2));

    private static ReminderDecision Evaluate(Cadence cadence, DateTimeOffset now, DateOnly? latest = null) =>
        ReminderPolicy.Evaluate(cadence, now, latest);

    [Theory]
    [InlineData(17, 59, false)]
    [InlineData(18, 0, true)]
    [InlineData(23, 45, true)]
    public void Daily_from_18_on_trading_days(int hour, int minute, bool due) =>
        Assert.Equal(new ReminderDecision("D:2026-09-21", due), Evaluate(Cadence.Daily, At(2026, 9, 21, hour, minute)));

    [Fact]
    public void Daily_not_due_after_checking_in_today() =>
        Assert.False(Evaluate(Cadence.Daily, At(2026, 9, 21, 18), D(2026, 9, 21)).IsDue);

    [Fact]
    public void Daily_due_when_last_check_in_was_yesterday() =>
        Assert.True(Evaluate(Cadence.Daily, At(2026, 9, 22, 18), D(2026, 9, 21)).IsDue);

    [Fact]
    public void Daily_never_on_weekends() =>
        Assert.False(Evaluate(Cadence.Daily, At(2026, 9, 26, 19)).IsDue);

    [Theory]
    [InlineData(2026, 9, 24, 20, false)] // Thursday
    [InlineData(2026, 9, 25, 17, false)] // Friday before 18
    [InlineData(2026, 9, 25, 18, true)]
    [InlineData(2026, 9, 27, 12, true)]  // Sunday catch-up, same window
    public void Weekly_from_friday_18(int y, int m, int d, int hour, bool due) =>
        Assert.Equal(new ReminderDecision("W:2026-09-21", due), Evaluate(Cadence.Weekly, At(y, m, d, hour)));

    [Theory]
    [InlineData(2026, 9, 21, false)] // Monday this week
    [InlineData(2026, 9, 20, true)]  // Sunday last week
    public void Weekly_counts_check_ins_since_monday(int y, int m, int d, bool due) =>
        Assert.Equal(due, Evaluate(Cadence.Weekly, At(2026, 9, 25, 18), D(y, m, d)).IsDue);

    // 2026-W53 runs Mon 28 Dec – Sun 3 Jan and is split at New Year into two windows.
    [Theory]
    [InlineData(2026, 12, 30, 20, "W:2026-12-28", false)] // Wednesday
    [InlineData(2026, 12, 31, 17, "W:2026-12-28", false)]
    [InlineData(2026, 12, 31, 18, "W:2026-12-28", true)]  // last trading day of the season
    [InlineData(2027, 1, 1, 17, "W:2027-01-01", false)]
    [InlineData(2027, 1, 1, 18, "W:2027-01-01", true)]    // the new season's part, reminded on Friday
    [InlineData(2027, 1, 3, 12, "W:2027-01-01", true)]
    public void Week_split_at_new_year_has_a_window_per_season(int y, int m, int d, int hour, string key, bool due) =>
        Assert.Equal(new ReminderDecision(key, due), Evaluate(Cadence.Weekly, At(y, m, d, hour)));

    [Fact]
    public void Check_ins_in_the_old_season_do_not_count_for_the_new_seasons_part()
    {
        Assert.False(Evaluate(Cadence.Weekly, At(2026, 12, 31, 18), D(2026, 12, 29)).IsDue);
        Assert.True(Evaluate(Cadence.Weekly, At(2027, 1, 1, 18), D(2026, 12, 31)).IsDue);
        Assert.False(Evaluate(Cadence.Weekly, At(2027, 1, 1, 18), D(2027, 1, 1)).IsDue);
    }

    [Fact]
    public void Season_part_of_a_week_without_trading_days_gets_no_reminder()
    {
        // 2028 starts on a Saturday: its part of 2027-W52 is only the weekend.
        Assert.Equal(new ReminderDecision("W:2028-01-01", false), Evaluate(Cadence.Weekly, At(2028, 1, 1, 18)));
        Assert.False(Evaluate(Cadence.Weekly, At(2028, 1, 2, 18)).IsDue);
        // The 2027 part (Mon 27 – Fri 31 Dec) is reminded on Friday as usual.
        Assert.Equal(new ReminderDecision("W:2027-12-27", true), Evaluate(Cadence.Weekly, At(2027, 12, 31, 18)));
    }

    [Theory]
    [InlineData(2026, 9, 29, 20, false)] // Tuesday
    [InlineData(2026, 9, 30, 17, false)] // last weekday, before 18
    [InlineData(2026, 9, 30, 18, true)]
    public void Monthly_on_last_weekday_of_month(int y, int m, int d, int hour, bool due) =>
        Assert.Equal(new ReminderDecision("M:2026-09", due), Evaluate(Cadence.Monthly, At(y, m, d, hour)));

    [Fact]
    public void Monthly_last_weekday_skips_weekend()
    {
        // October 2026 ends on Saturday; the reminder is Friday the 30th, and the weekend still catches up.
        Assert.True(Evaluate(Cadence.Monthly, At(2026, 10, 30, 18)).IsDue);
        Assert.True(Evaluate(Cadence.Monthly, At(2026, 10, 31, 10)).IsDue);
        Assert.False(Evaluate(Cadence.Monthly, At(2026, 10, 29, 18)).IsDue);
    }

    [Fact]
    public void Monthly_not_due_after_check_in_this_month() =>
        Assert.False(Evaluate(Cadence.Monthly, At(2026, 9, 30, 18), D(2026, 9, 1)).IsDue);

    [Fact]
    public void Unknown_cadence_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Evaluate((Cadence)9, At(2026, 9, 21, 18)));
}
