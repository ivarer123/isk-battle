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
        Assert.Equal(new ReminderDecision("W:2026-39", due), Evaluate(Cadence.Weekly, At(y, m, d, hour)));

    [Theory]
    [InlineData(2026, 9, 21, false)] // Monday this week
    [InlineData(2026, 9, 20, true)]  // Sunday last week
    public void Weekly_counts_check_ins_since_monday(int y, int m, int d, bool due) =>
        Assert.Equal(due, Evaluate(Cadence.Weekly, At(2026, 9, 25, 18), D(y, m, d)).IsDue);

    [Fact]
    public void Weekly_key_uses_iso_week_year() =>
        Assert.Equal("W:2026-53", Evaluate(Cadence.Weekly, At(2027, 1, 1, 18)).WindowKey);

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
