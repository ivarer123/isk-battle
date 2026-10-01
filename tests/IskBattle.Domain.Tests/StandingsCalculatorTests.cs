using IskBattle.Domain.Entities;
using IskBattle.Domain.Periods;
using IskBattle.Domain.Standings;
using static IskBattle.Domain.Tests.TestData;

namespace IskBattle.Domain.Tests;

public class StandingsCalculatorTests
{
    private static readonly StandingsMember Anna = new(Guid.NewGuid(), "Anna");
    private static readonly StandingsMember Bertil = new(Guid.NewGuid(), "Bertil");
    private static readonly StandingsMember Cecilia = new(Guid.NewGuid(), "Cecilia");
    private static readonly StandingsMember David = new(Guid.NewGuid(), "David");

    private static IReadOnlyList<StandingsRow> Calculate(
        PeriodType type, DateOnly date, IEnumerable<StandingsMember> members, params CheckIn[] checkIns) =>
        StandingsCalculator.Calculate(PeriodCalculator.For(type, date), members, checkIns);

    private static StandingsRow Row(IReadOnlyList<StandingsRow> rows, StandingsMember m) =>
        Assert.Single(rows, r => r.UserId == m.UserId);

    [Fact]
    public void First_check_in_of_season_uses_base_zero()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 1, 2), [Anna],
            CheckIn(Anna.UserId, D(2025, 12, 30), 25m),  // previous season is ignored
            CheckIn(Anna.UserId, D(2026, 1, 2), 1.5m));

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, 1.5m, 1.5m), Row(rows, Anna));
    }

    [Fact]
    public void Period_return_compounds_from_base()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 21), [Anna],
            CheckIn(Anna.UserId, D(2026, 9, 18), 10m),
            CheckIn(Anna.UserId, D(2026, 9, 25), 12.2m));

        // 1.122 / 1.10 - 1 = 2.00 %
        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, 2.00m, 12.2m), Row(rows, Anna));
    }

    [Fact]
    public void Latest_and_base_are_the_most_recent_check_ins()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 21), [Anna],
            CheckIn(Anna.UserId, D(2026, 9, 25), 7m),
            CheckIn(Anna.UserId, D(2026, 9, 1), 100m),
            CheckIn(Anna.UserId, D(2026, 9, 21), 6m),
            CheckIn(Anna.UserId, D(2026, 9, 14), 5m),
            CheckIn(Anna.UserId, D(2026, 9, 28), 50m)); // after the period

        // base 5 (14 Sep), latest 7 (25 Sep): 1.07 / 1.05 - 1 = 1.904...%
        Assert.Equal(1.90m, Row(rows, Anna).PeriodReturn);
        Assert.Equal(7m, Row(rows, Anna).YtdPercent);
    }

    [Fact]
    public void Mid_season_joiner_is_Ny_for_the_period_but_ranked_for_the_year()
    {
        var checkIns = new[] { CheckIn(Anna.UserId, D(2026, 9, 23), 8m) };

        var week = Calculate(PeriodType.Week, D(2026, 9, 21), [Anna], checkIns);
        var year = Calculate(PeriodType.Year, D(2026, 9, 21), [Anna], checkIns);

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ny, null, null, 8m), Row(week, Anna));
        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, 8m, 8m), Row(year, Anna));
    }

    [Fact]
    public void Joiner_is_ranked_from_their_second_period()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 28), [Anna],
            CheckIn(Anna.UserId, D(2026, 9, 23), 8m),
            CheckIn(Anna.UserId, D(2026, 10, 2), 9.08m));

        Assert.Equal(StandingStatus.Ranked, Row(rows, Anna).Status);
        Assert.Equal(1.00m, Row(rows, Anna).PeriodReturn);
    }

    [Fact]
    public void Absent_player_is_Pausad_with_last_known_ytd()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 21), [Anna, Bertil],
            CheckIn(Anna.UserId, D(2026, 9, 14), 3m),
            CheckIn(Anna.UserId, D(2026, 9, 22), 4m),
            CheckIn(Bertil.UserId, D(2026, 9, 1), 10m),
            CheckIn(Bertil.UserId, D(2026, 9, 11), 11m));

        Assert.Equal(new StandingsRow(Bertil.UserId, "Bertil", StandingStatus.Pausad, null, null, 11m), Row(rows, Bertil));
        Assert.Equal(1, Row(rows, Anna).Rank);
    }

    [Fact]
    public void Player_rejoins_rankings_at_next_check_in()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 28), [Anna],
            CheckIn(Anna.UserId, D(2026, 9, 4), 10m),   // then away for two weeks
            CheckIn(Anna.UserId, D(2026, 9, 29), 21m));

        Assert.Equal(StandingStatus.Ranked, Row(rows, Anna).Status);
        Assert.Equal(10.00m, Row(rows, Anna).PeriodReturn);
    }

    [Fact]
    public void No_check_ins_mid_season_is_Ny_not_Pausad()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 21), [Anna],
            CheckIn(Anna.UserId, D(2025, 12, 31), 30m)); // last season does not count

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ny, null, null, null), Row(rows, Anna));
    }

    [Fact]
    public void No_check_ins_in_season_is_Pausad_for_the_year()
    {
        var rows = Calculate(PeriodType.Year, D(2026, 9, 21), [Anna],
            CheckIn(Anna.UserId, D(2025, 12, 31), 30m));

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Pausad, null, null, null), Row(rows, Anna));
    }

    [Fact]
    public void No_check_in_yet_in_first_period_is_Pausad()
    {
        var rows = Calculate(PeriodType.Month, D(2026, 1, 15), [Anna]);

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Pausad, null, null, null), Row(rows, Anna));
    }

    [Fact]
    public void Year_view_with_a_single_check_in()
    {
        var rows = Calculate(PeriodType.Year, D(2026, 12, 31), [Anna],
            CheckIn(Anna.UserId, D(2026, 3, 9), -4.25m));

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, -4.25m, -4.25m), Row(rows, Anna));
    }

    [Fact]
    public void Year_view_uses_latest_ytd_directly()
    {
        var rows = Calculate(PeriodType.Year, D(2026, 2, 1), [Anna],
            CheckIn(Anna.UserId, D(2026, 1, 5), 2m),
            CheckIn(Anna.UserId, D(2026, 11, 30), 14.567m),
            CheckIn(Anna.UserId, D(2027, 1, 4), 1m));

        Assert.Equal(14.57m, Row(rows, Anna).PeriodReturn);
        Assert.Equal(14.567m, Row(rows, Anna).YtdPercent);
    }

    [Fact]
    public void Ties_share_a_rank_with_competition_ranking()
    {
        var rows = Calculate(PeriodType.Year, D(2026, 9, 21), [David, Cecilia, Bertil, Anna],
            CheckIn(Anna.UserId, D(2026, 9, 21), 5m),
            CheckIn(Bertil.UserId, D(2026, 9, 21), 7m),
            CheckIn(Cecilia.UserId, D(2026, 9, 21), 5m),
            CheckIn(David.UserId, D(2026, 9, 21), 7m));

        Assert.Equal(
            [("Bertil", 1), ("David", 1), ("Anna", 3), ("Cecilia", 3)],
            rows.Select(r => (r.DisplayName, r.Rank!.Value)));
    }

    [Fact]
    public void Ties_compare_rounded_returns()
    {
        var rows = Calculate(PeriodType.Year, D(2026, 9, 21), [Anna, Bertil, Cecilia],
            CheckIn(Anna.UserId, D(2026, 9, 21), 1.2349m),
            CheckIn(Bertil.UserId, D(2026, 9, 21), 1.2251m),
            CheckIn(Cecilia.UserId, D(2026, 9, 21), 1.225m)); // rounds away from zero to 1.23

        Assert.All(rows, r => Assert.Equal((1, 1.23m), (r.Rank!.Value, r.PeriodReturn!.Value)));
    }

    [Fact]
    public void Same_name_tie_falls_back_to_user_id()
    {
        var a = new StandingsMember(new Guid("00000000-0000-0000-0000-000000000002"), "Kim");
        var b = new StandingsMember(new Guid("00000000-0000-0000-0000-000000000001"), "kim");

        var rows = Calculate(PeriodType.Year, D(2026, 9, 21), [a, b],
            CheckIn(a.UserId, D(2026, 9, 21), 1m),
            CheckIn(b.UserId, D(2026, 9, 21), 1m));

        Assert.Equal([b.UserId, a.UserId], rows.Select(r => r.UserId));
    }

    [Fact]
    public void Unranked_listed_below_ranked_Ny_before_Pausad()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 9, 21), [Anna, Bertil, Cecilia, David],
            CheckIn(Anna.UserId, D(2026, 9, 1), 1m),   // Pausad
            CheckIn(Bertil.UserId, D(2026, 9, 22), 1m), // Ny
            CheckIn(Cecilia.UserId, D(2026, 9, 1), 1m),
            CheckIn(Cecilia.UserId, D(2026, 9, 22), -20m)); // Ranked despite a big loss
        // David: no check-ins at all → Ny

        Assert.Equal(
            [("Cecilia", StandingStatus.Ranked), ("Bertil", StandingStatus.Ny),
             ("David", StandingStatus.Ny), ("Anna", StandingStatus.Pausad)],
            rows.Select(r => (r.DisplayName, r.Status)));
        Assert.All(rows.Skip(1), r => Assert.Null(r.Rank));
    }

    [Fact]
    public void Negative_ytd_values()
    {
        var rows = Calculate(PeriodType.Month, D(2026, 9, 1), [Anna, Bertil, Cecilia],
            CheckIn(Anna.UserId, D(2026, 8, 31), -10m),
            CheckIn(Anna.UserId, D(2026, 9, 30), -5.5m),     // 0.945 / 0.90 - 1 = +5 %
            CheckIn(Bertil.UserId, D(2026, 8, 31), 10m),
            CheckIn(Bertil.UserId, D(2026, 9, 30), -1m),     // 0.99 / 1.10 - 1 = -10 %
            CheckIn(Cecilia.UserId, D(2026, 8, 31), -20m),
            CheckIn(Cecilia.UserId, D(2026, 9, 30), -28m));  // 0.72 / 0.80 - 1 = -10 %

        Assert.Equal(
            [("Anna", 1, 5.00m), ("Bertil", 2, -10.00m), ("Cecilia", 2, -10.00m)],
            rows.Select(r => (r.DisplayName, r.Rank!.Value, r.PeriodReturn!.Value)));
    }

    [Fact]
    public void Minimum_base_does_not_divide_by_zero()
    {
        var rows = Calculate(PeriodType.Month, D(2026, 9, 1), [Anna],
            CheckIn(Anna.UserId, D(2026, 8, 31), GameConfig.MinYtdPercent),
            CheckIn(Anna.UserId, D(2026, 9, 30), -99.98m));

        Assert.Equal(100.00m, Row(rows, Anna).PeriodReturn); // 0.0002 / 0.0001 - 1
    }

    [Fact]
    public void January_1_day_uses_base_zero_and_ignores_previous_season()
    {
        var rows = Calculate(PeriodType.Day, D(2027, 1, 1), [Anna],
            CheckIn(Anna.UserId, D(2026, 12, 31), 18m),
            CheckIn(Anna.UserId, D(2027, 1, 1), 0.4m));

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, 0.4m, 0.4m), Row(rows, Anna));
    }

    [Fact]
    public void Cross_year_week_2027_side_ignores_december_check_ins()
    {
        var rows = Calculate(PeriodType.Week, D(2027, 1, 2), [Anna],
            CheckIn(Anna.UserId, D(2026, 12, 28), 18m),
            CheckIn(Anna.UserId, D(2027, 1, 3), -0.6m));

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, -0.6m, -0.6m), Row(rows, Anna));
    }

    [Fact]
    public void Cross_year_week_2026_side_ignores_january_check_ins()
    {
        var rows = Calculate(PeriodType.Week, D(2026, 12, 30), [Anna, Bertil],
            CheckIn(Anna.UserId, D(2026, 12, 23), 10m),
            CheckIn(Anna.UserId, D(2026, 12, 30), 12.2m),
            CheckIn(Anna.UserId, D(2027, 1, 3), 0.5m),
            CheckIn(Bertil.UserId, D(2026, 12, 23), 10m),
            CheckIn(Bertil.UserId, D(2027, 1, 2), 0.5m));

        Assert.Equal(2.00m, Row(rows, Anna).PeriodReturn);
        Assert.Equal(new StandingsRow(Bertil.UserId, "Bertil", StandingStatus.Pausad, null, null, 10m), Row(rows, Bertil));
    }

    [Fact]
    public void First_trading_week_after_weekend_new_year_uses_base_zero()
    {
        // 1–2 Jan 2028 are a weekend, so the week of Mon 3 Jan has no earlier trading day in the season.
        var rows = Calculate(PeriodType.Week, D(2028, 1, 5), [Anna],
            CheckIn(Anna.UserId, D(2028, 1, 5), 0.8m));

        Assert.Equal(new StandingsRow(Anna.UserId, "Anna", StandingStatus.Ranked, 1, 0.8m, 0.8m), Row(rows, Anna));
    }

    [Fact]
    public void Weekend_check_in_counts_for_friday_and_is_base_for_monday()
    {
        CheckIn[] checkIns =
        [
            CheckIn(Anna.UserId, D(2026, 9, 24), 10m), // Thursday
            CheckIn(Anna.UserId, D(2026, 9, 26), 21m), // Saturday
            CheckIn(Anna.UserId, D(2026, 9, 28), 23.42m), // Monday
        ];

        var friday = Calculate(PeriodType.Day, D(2026, 9, 25), [Anna], checkIns);
        var monday = Calculate(PeriodType.Day, D(2026, 9, 28), [Anna], checkIns);

        Assert.Equal(10.00m, Row(friday, Anna).PeriodReturn);
        Assert.Equal(2.00m, Row(monday, Anna).PeriodReturn);
    }

    [Fact]
    public void Duplicate_date_uses_most_recently_recorded()
    {
        var at = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

        var rows = Calculate(PeriodType.Year, D(2026, 9, 22), [Anna],
            CheckIn(Anna.UserId, D(2026, 9, 22), 6m, at.AddHours(1)),
            CheckIn(Anna.UserId, D(2026, 9, 22), 5m, at));

        Assert.Equal(6m, Row(rows, Anna).PeriodReturn);
    }

    [Fact]
    public void Check_ins_of_non_members_are_ignored()
    {
        var rows = Calculate(PeriodType.Year, D(2026, 9, 21), [Anna],
            CheckIn(Anna.UserId, D(2026, 9, 21), 1m),
            CheckIn(Bertil.UserId, D(2026, 9, 21), 50m));

        Assert.Equal(Anna.UserId, Assert.Single(rows).UserId);
    }

    [Fact]
    public void No_members_gives_empty_standings() =>
        Assert.Empty(Calculate(PeriodType.Week, D(2026, 9, 21), []));

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(12.40, 0, 12.40)]
    [InlineData(-3.15, 0, -3.15)]
    [InlineData(21, 10, 10)]
    [InlineData(0, 25, -20)]
    [InlineData(1000, 0, 1000)]
    public void PeriodReturn_formula(double latest, double @base, double expected) =>
        Assert.Equal((decimal)expected, StandingsCalculator.PeriodReturn((decimal)latest, (decimal)@base));
}
