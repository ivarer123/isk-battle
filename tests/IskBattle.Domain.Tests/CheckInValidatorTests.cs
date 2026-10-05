using IskBattle.Domain.CheckIns;
using IskBattle.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class CheckInValidatorTests
{
    private static readonly Guid User = Guid.NewGuid();

    // Monday 2026-09-21 22:45 in Stockholm (CEST, UTC+2): the Daily window for that day is open.
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 21, 20, 45, 0, TimeSpan.Zero));

    private Cadence[] _cadences = [];

    private CheckInValidation Validate(decimal ytd, bool confirmed = false, params CheckIn[] history) =>
        CheckInValidator.Validate(ytd, history, _time, _ => _cadences, confirmed);

    [Theory]
    [InlineData(-99.99)]
    [InlineData(0)]
    [InlineData(1000)]
    public void Accepts_values_in_range(double ytd)
    {
        var result = Validate((decimal)ytd);

        Assert.Equal(new CheckInValidation(CheckInOutcome.Accepted, D(2026, 9, 21), false, null), result);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(1000.01)]
    public void Rejects_values_out_of_range(double ytd) =>
        Assert.Equal(CheckInOutcome.OutOfRange, Validate((decimal)ytd, confirmed: true).Outcome);

    [Fact]
    public void Jump_of_exactly_threshold_is_accepted() =>
        Assert.Equal(CheckInOutcome.Accepted, Validate(30m, false, CheckIn(User, D(2026, 9, 18), 10m)).Outcome);

    [Theory]
    [InlineData(30.01)]
    [InlineData(-10.01)]
    public void Jump_above_threshold_requires_confirmation(double ytd)
    {
        var result = Validate((decimal)ytd, false, CheckIn(User, D(2026, 9, 18), 10m));

        Assert.Equal(new CheckInValidation(CheckInOutcome.RequiresConfirmation, D(2026, 9, 21), false, 10m), result);
    }

    [Fact]
    public void Confirmed_jump_is_accepted() =>
        Assert.Equal(CheckInOutcome.Accepted, Validate(50m, true, CheckIn(User, D(2026, 9, 18), 10m)).Outcome);

    [Fact]
    public void Compares_with_most_recent_previous_date()
    {
        var result = Validate(31m, false,
            CheckIn(User, D(2026, 9, 1), 30m),
            CheckIn(User, D(2026, 9, 18), 10m),
            CheckIn(User, D(2026, 9, 10), 25m));

        Assert.Equal((CheckInOutcome.RequiresConfirmation, (decimal?)10m), (result.Outcome, result.PreviousYtdPercent));
    }

    [Fact]
    public void Overwriting_within_the_window_compares_with_the_previous_trading_day()
    {
        var result = Validate(12m, false,
            CheckIn(User, D(2026, 9, 18), 10m),
            CheckIn(User, D(2026, 9, 21), 40m)); // a typo being corrected while the window is open

        Assert.Equal(new CheckInValidation(CheckInOutcome.Accepted, D(2026, 9, 21), true, 10m), result);
    }

    [Fact]
    public void First_check_in_of_season_never_requires_confirmation()
    {
        _time.SetUtcNow(new DateTimeOffset(2027, 1, 4, 21, 45, 0, TimeSpan.Zero)); // Monday 22:45 CET

        var result = Validate(0.3m, false, CheckIn(User, D(2026, 12, 31), 45m));

        Assert.Equal(new CheckInValidation(CheckInOutcome.Accepted, D(2027, 1, 4), false, null), result);
    }

    [Fact]
    public void Morning_check_in_reports_on_the_previous_trading_day()
    {
        _time.SetUtcNow(new DateTimeOffset(2026, 9, 22, 6, 30, 0, TimeSpan.Zero)); // Tuesday 08:30 CEST

        Assert.Equal((CheckInOutcome.Accepted, D(2026, 9, 21)), (Validate(1m).Outcome, Validate(1m).ForDate));
    }

    [Fact]
    public void Outside_a_window_is_rejected_with_the_next_opening()
    {
        _time.SetUtcNow(new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero)); // Tuesday 12:00 CEST

        var result = Validate(1m);

        Assert.Equal(CheckInOutcome.WindowClosed, result.Outcome);
        Assert.Equal(new DateTime(2026, 9, 22, 22, 30, 0), result.NextWindow!.Opens);
    }

    [Fact]
    public void Weekly_only_player_cannot_check_in_midweek_but_can_on_saturday()
    {
        _cadences = [Cadence.Weekly];

        Assert.Equal(CheckInOutcome.WindowClosed, Validate(1m).Outcome); // Monday 22:45
        _time.SetUtcNow(new DateTimeOffset(2026, 9, 26, 13, 0, 0, TimeSpan.Zero)); // Saturday 15:00
        Assert.Equal((CheckInOutcome.Accepted, D(2026, 9, 25)), (Validate(1m).Outcome, Validate(1m).ForDate));
    }
}
