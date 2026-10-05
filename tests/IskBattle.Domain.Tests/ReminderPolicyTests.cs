using IskBattle.Domain.Reminders;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class ReminderPolicyTests
{
    // Offsets don't matter: the policy reads the Stockholm wall-clock time.
    private static DateTimeOffset At(int y, int m, int d, int hour, int minute = 0) =>
        new(y, m, d, hour, minute, 0, TimeSpan.FromHours(2));

    private static ReminderDecision Evaluate(Cadence cadence, DateTimeOffset now, DateOnly? latest = null) =>
        ReminderPolicy.Evaluate(cadence, now, latest);

    [Theory]
    [InlineData(2026, 9, 22, 7, 59, false)]
    [InlineData(2026, 9, 22, 8, 0, true)]  // Tuesday 08:00, reporting on Monday
    [InlineData(2026, 9, 22, 8, 45, true)] // catch-up inside the window
    [InlineData(2026, 9, 22, 9, 0, false)] // window closed
    public void Daily_at_0800_for_the_previous_trading_day(int y, int m, int d, int hour, int minute, bool due) =>
        Assert.Equal(new ReminderDecision("D:2026-09-21", due), Evaluate(Cadence.Daily, At(y, m, d, hour, minute)));

    [Fact]
    public void Daily_on_saturday_for_friday_and_never_on_sunday_or_monday_morning()
    {
        Assert.Equal(new ReminderDecision("D:2026-09-25", true), Evaluate(Cadence.Daily, At(2026, 9, 26, 8)));
        Assert.False(Evaluate(Cadence.Daily, At(2026, 9, 27, 8)).IsDue);
        Assert.False(Evaluate(Cadence.Daily, At(2026, 9, 28, 8)).IsDue);
    }

    [Theory]
    [InlineData(2026, 9, 21, false)] // checked in for that trading day
    [InlineData(2026, 9, 18, true)]  // last check-in was the Friday before
    public void Daily_only_when_the_trading_day_is_missing(int y, int m, int d, bool due) =>
        Assert.Equal(due, Evaluate(Cadence.Daily, At(2026, 9, 22, 8), D(y, m, d)).IsDue);

    [Theory]
    [InlineData(2026, 9, 26, 7, false)]
    [InlineData(2026, 9, 26, 8, true)]  // Saturday 08:00
    [InlineData(2026, 9, 27, 23, true)] // Sunday catch-up
    [InlineData(2026, 9, 28, 8, false)] // Monday: the window has closed
    public void Weekly_on_saturday_for_friday(int y, int m, int d, int hour, bool due) =>
        Assert.Equal(new ReminderDecision("W:2026-09-25", due), Evaluate(Cadence.Weekly, At(y, m, d, hour)));

    [Fact]
    public void Weekly_never_midweek() =>
        Assert.False(Evaluate(Cadence.Weekly, At(2026, 9, 23, 8)).IsDue);

    [Fact]
    public void Monthly_the_day_after_the_last_trading_day()
    {
        Assert.Equal(new ReminderDecision("M:2026-09-30", true), Evaluate(Cadence.Monthly, At(2026, 10, 1, 8)));
        Assert.False(Evaluate(Cadence.Monthly, At(2026, 9, 30, 8)).IsDue);
        // October ends on a Saturday: last trading day Friday 30 Oct, reminder Saturday, catch-up until Monday 09:00.
        Assert.True(Evaluate(Cadence.Monthly, At(2026, 10, 31, 8)).IsDue);
        Assert.True(Evaluate(Cadence.Monthly, At(2026, 11, 2, 8, 30)).IsDue);
        Assert.False(Evaluate(Cadence.Monthly, At(2026, 11, 2, 9)).IsDue);
    }

    [Fact]
    public void Monthly_not_due_after_check_in_for_the_last_trading_day() =>
        Assert.False(Evaluate(Cadence.Monthly, At(2026, 10, 1, 8), D(2026, 9, 30)).IsDue);

    [Theory]
    [InlineData(Cadence.Daily, "D:2026-12-31")]
    [InlineData(Cadence.Weekly, "W:2026-12-31")]
    [InlineData(Cadence.Monthly, "M:2026-12-31")]
    public void Season_end_reminder_goes_out_at_2000_on_the_last_trading_day(Cadence cadence, string key)
    {
        Assert.False(Evaluate(cadence, At(2026, 12, 31, 19, 59)).IsDue);
        Assert.Equal(new ReminderDecision(key, true), Evaluate(cadence, At(2026, 12, 31, 20)));
        Assert.True(Evaluate(cadence, At(2026, 12, 31, 23, 59)).IsDue);
        Assert.False(Evaluate(cadence, At(2027, 1, 1, 8)).IsDue); // the season's window closed at midnight
    }

    [Fact]
    public void New_seasons_first_friday_is_reminded_on_saturday() =>
        Assert.Equal(new ReminderDecision("W:2027-01-01", true), Evaluate(Cadence.Weekly, At(2027, 1, 2, 8)));
}
