using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;
using IskBattle.Domain.Periods;

namespace IskBattle.Domain.Tests;

public class LeagueRulesTests
{
    [Fact]
    public void Queued_cadence_applies_from_next_season()
    {
        var league = new League { Cadence = Cadence.Weekly };

        league.QueueCadence(Cadence.Daily, 2026);

        Assert.Equal(Cadence.Weekly, league.CadenceFor(2026));
        Assert.Equal(Cadence.Daily, league.CadenceFor(2027));
        Assert.Equal(Cadence.Daily, league.CadenceFor(2028));
    }

    [Fact]
    public void Queueing_in_a_later_season_folds_in_the_applied_change()
    {
        var league = new League { Cadence = Cadence.Weekly };
        league.QueueCadence(Cadence.Daily, 2026);

        league.QueueCadence(Cadence.Monthly, 2027);

        Assert.Equal((Cadence.Daily, Cadence.Monthly, 2028),
            (league.Cadence, league.NextSeasonCadence!.Value, league.NextCadenceSeason!.Value));
        Assert.Equal(Cadence.Daily, league.CadenceFor(2027));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Cadence.Weekly)] // same as current
    public void Queueing_null_or_current_clears_the_queue(Cadence? cadence)
    {
        var league = new League { Cadence = Cadence.Weekly };
        league.QueueCadence(Cadence.Daily, 2026);

        league.QueueCadence(cadence, 2026);

        Assert.Equal((Cadence.Weekly, (Cadence?)null, (int?)null),
            (league.Cadence, league.NextSeasonCadence, league.NextCadenceSeason));
    }

    [Fact]
    public void Visible_periods_follow_cadence()
    {
        Assert.Equal([PeriodType.Day, PeriodType.Week, PeriodType.Month, PeriodType.Year], LeagueRules.VisiblePeriods(Cadence.Daily));
        Assert.Equal([PeriodType.Week, PeriodType.Month, PeriodType.Year], LeagueRules.VisiblePeriods(Cadence.Weekly));
        Assert.Equal([PeriodType.Month, PeriodType.Year], LeagueRules.VisiblePeriods(Cadence.Monthly));
        Assert.Throws<ArgumentOutOfRangeException>(() => LeagueRules.VisiblePeriods((Cadence)9));
    }

    [Fact]
    public void Most_frequent_cadence_wins()
    {
        Assert.Equal(Cadence.Daily, LeagueRules.MostFrequent([Cadence.Monthly, Cadence.Daily, Cadence.Weekly]));
        Assert.Equal(Cadence.Weekly, LeagueRules.MostFrequent([Cadence.Monthly, Cadence.Weekly]));
        Assert.Null(LeagueRules.MostFrequent([]));
    }

    [Fact]
    public void Upgrade_options_are_higher_tiers_at_the_difference()
    {
        var league = new League { Tier = LeagueTier.Premium50 };

        Assert.Equal(
            [(LeagueTier.Premium100, 100, 10_000), (LeagueTier.Premium250, 250, 30_000)],
            league.UpgradeOptions());
        Assert.Empty(new League { Tier = LeagueTier.Premium250 }.UpgradeOptions());
    }

    [Fact]
    public void Invite_codes_use_the_unambiguous_alphabet()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => InviteCode.New()).ToList();

        Assert.All(codes, c => Assert.Equal(c, InviteCode.Normalize(c)));
        Assert.True(codes.Distinct().Count() > 190);
    }

    [Theory]
    [InlineData(" abcd-efgh ", "ABCDEFGH")]
    [InlineData("ABCD EF23", "ABCDEF23")]
    [InlineData("ABCDEFG", null)]      // too short
    [InlineData("ABCDEFG0", null)]     // ambiguous character
    [InlineData(null, null)]
    public void Invite_code_normalization(string? input, string? expected) =>
        Assert.Equal(expected, InviteCode.Normalize(input));
}
