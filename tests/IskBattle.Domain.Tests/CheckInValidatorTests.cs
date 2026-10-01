using IskBattle.Domain.CheckIns;
using IskBattle.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class CheckInValidatorTests
{
    private static readonly Guid User = Guid.NewGuid();

    // 2026-09-21 12:00 in Stockholm (CEST, UTC+2).
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero));

    private CheckInValidation Validate(decimal ytd, bool confirmed = false, params CheckIn[] history) =>
        CheckInValidator.Validate(ytd, history, _time, confirmed);

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
    public void Overwriting_today_compares_with_previous_date_not_today()
    {
        var result = Validate(12m, false,
            CheckIn(User, D(2026, 9, 18), 10m),
            CheckIn(User, D(2026, 9, 21), 40m)); // today's typo being corrected

        Assert.Equal(new CheckInValidation(CheckInOutcome.Accepted, D(2026, 9, 21), true, 10m), result);
    }

    [Fact]
    public void First_check_in_of_season_never_requires_confirmation()
    {
        _time.SetUtcNow(new DateTimeOffset(2027, 1, 4, 17, 0, 0, TimeSpan.Zero));

        var result = Validate(0.3m, false, CheckIn(User, D(2026, 12, 31), 45m));

        Assert.Equal(new CheckInValidation(CheckInOutcome.Accepted, D(2027, 1, 4), false, null), result);
    }

    [Fact]
    public void Today_is_the_stockholm_date()
    {
        // 23:30 UTC on 31 Dec is 00:30 on 1 Jan in Stockholm (CET, UTC+1).
        _time.SetUtcNow(new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal(D(2027, 1, 1), Validate(1m).ForDate);
    }
}
