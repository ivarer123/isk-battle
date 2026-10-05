using System.Net;
using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;
using IskBattle.Domain.Periods;
using IskBattle.Domain.Standings;

namespace IskBattle.Api.Tests.Api;

public class StandingsTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    [Fact]
    public async Task Weekly_standings_rank_players_and_show_ny_and_pausad()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        var cecilia = await JoinedPlayerAsync("Cecilia", league);
        var david = await JoinedPlayerAsync("David", league);

        SetDate(2026, 9, 18); // previous Friday
        await CheckInAsync(anna, 10m);
        await CheckInAsync(bertil, 5m);
        await CheckInAsync(david, 3m);
        SetDate(2026, 9, 25); // weekly check-ins report on Fridays
        await CheckInAsync(anna, 12.2m);   // +2.00 %
        await CheckInAsync(bertil, 7.1m);  // +2.00 %
        await CheckInAsync(cecilia, 4m);   // new this week
        // David is away this week.

        var standings = await anna.GetAsync<StandingsResponse>($"/leagues/{league.Id}/standings?period=week&date=2026-09-25");

        Assert.Equal(new PeriodResponse(PeriodType.Week, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 28), 2026, 39),
            standings.Period);
        Assert.Equal(
            [("Anna", StandingStatus.Ranked, (int?)1, (decimal?)2.00m, true),
             ("Bertil", StandingStatus.Ranked, 1, 2.00m, false),
             ("Cecilia", StandingStatus.Ny, null, null, false),
             ("David", StandingStatus.Pausad, null, null, false)],
            standings.Rows.Select(r => (r.DisplayName, r.Status, r.Rank, r.PeriodReturn, r.IsMe)));
        Assert.Equal(3m, standings.Rows[3].YtdPercent);
    }

    [Fact]
    public async Task Year_is_the_default_and_ranks_by_ytd()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna, Cadence.Monthly);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        SetDate(2026, 9, 30); // last trading day of the month
        await CheckInAsync(anna, -3.15m);
        await CheckInAsync(bertil, 12.4m);

        var standings = await bertil.GetAsync<StandingsResponse>($"/leagues/{league.Id}/standings");

        Assert.Equal(PeriodType.Year, standings.Period.Type);
        Assert.Equal(["Bertil", "Anna"], standings.Rows.Select(r => r.DisplayName));
    }

    [Fact]
    public async Task Pending_members_are_not_in_the_standings()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        for (var i = 2; i <= 10; i++)
            await JoinedPlayerAsync($"Spelare {i}", league);
        var pending = await JoinedPlayerAsync("Elva", league, MemberStatus.Pending);
        await CheckInAsync(pending, 50m);

        var standings = await anna.GetAsync<StandingsResponse>($"/leagues/{league.Id}/standings?period=year");

        Assert.Equal(10, standings.Rows.Count);
        Assert.DoesNotContain(standings.Rows, r => r.DisplayName == "Elva");
    }

    [Theory]
    [InlineData("9999-12-31")]
    [InlineData("0001-01-01")]
    public async Task Dates_outside_supported_seasons_are_rejected(string date)
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);

        var response = await anna.GetAsync($"/leagues/{league.Id}/standings?date={date}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("date_invalid", await response.ProblemCodeAsync());
    }

    [Theory]
    [InlineData("day")]      // not shown for a weekly league
    [InlineData("quarter")]
    public async Task Periods_not_shown_for_the_league_are_rejected(string period)
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna, Cadence.Weekly);

        var response = await anna.GetAsync($"/leagues/{league.Id}/standings?period={period}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("period_invalid", await response.ProblemCodeAsync());
    }
}
