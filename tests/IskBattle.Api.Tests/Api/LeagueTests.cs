using System.Net;
using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;
using IskBattle.Domain.Periods;

namespace IskBattle.Api.Tests.Api;

public class LeagueTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    [Fact]
    public async Task Creator_is_owner_of_a_free_league()
    {
        var anna = await Factory.NewPlayerAsync("Anna");

        var league = await CreateLeagueAsync(anna, Cadence.Daily, "  Kompisligan  ");

        Assert.Equal(("Kompisligan", Cadence.Daily, LeagueTier.Free, 10, MemberRole.Owner, MemberStatus.Active),
            (league.Name, league.Cadence, league.Tier, league.Capacity, league.MyRole, league.MyStatus));
        Assert.Equal([PeriodType.Day, PeriodType.Week, PeriodType.Month, PeriodType.Year], league.Periods);
        Assert.Equal(8, league.InviteCode!.Length);
        Assert.Equal(
            [new UpgradeOption(LeagueTier.Premium50, 50, 9_900), new(LeagueTier.Premium100, 100, 19_900), new(LeagueTier.Premium250, 250, 39_900)],
            league.UpgradeOptions);
        Assert.Single(await anna.GetAsync<List<LeagueSummary>>("/leagues"));
    }

    [Fact]
    public async Task Invalid_league_name_is_rejected()
    {
        var anna = await Factory.NewPlayerAsync("Anna");

        var response = await anna.PostJsonAsync("/leagues", new { name = " ", cadence = Cadence.Weekly });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Joining_with_invite_code_any_format()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var bertil = await Factory.NewPlayerAsync("Bertil");
        var messyCode = $" {league.InviteCode![..4].ToLowerInvariant()}-{league.InviteCode[4..]} ";

        var joined = await (await bertil.PostJsonAsync("/leagues/join", new { inviteCode = messyCode })).ReadAsync<JoinLeagueResponse>();
        var again = await (await bertil.PostJsonAsync("/leagues/join", new { inviteCode = league.InviteCode })).ReadAsync<JoinLeagueResponse>();

        Assert.Equal(new JoinLeagueResponse(league.Id, "Kompisligan", MemberStatus.Active, false), joined);
        Assert.True(again.AlreadyMember);
        var detail = await bertil.GetAsync<LeagueDetail>($"/leagues/{league.Id}");
        Assert.Equal((MemberRole.Member, null, 2), (detail.MyRole, detail.InviteCode, detail.Members.Count));
    }

    [Fact]
    public async Task Unknown_invite_code_is_not_found()
    {
        var bertil = await Factory.NewPlayerAsync("Bertil");

        var response = await bertil.PostJsonAsync("/leagues/join", new { inviteCode = "ZZZZZZZZ" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("invite_not_found", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Eleventh_player_is_pending_and_hosts_are_notified()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        for (var i = 2; i <= 10; i++)
            await JoinedPlayerAsync($"Spelare {i}", league);

        var pending = await JoinedPlayerAsync("Elva", league, MemberStatus.Pending);

        var seenByPending = await pending.GetAsync<LeagueDetail>($"/leagues/{league.Id}");
        Assert.Equal((MemberStatus.Pending, 0, 10, 1), (seenByPending.MyStatus, seenByPending.Members.Count,
            seenByPending.ActiveMemberCount, seenByPending.PendingMemberCount));
        Assert.NotEmpty(seenByPending.UpgradeOptions); // a Pending member may pay
        Assert.Equal(HttpStatusCode.Forbidden, (await pending.GetAsync($"/leagues/{league.Id}/standings")).StatusCode);

        var seenByOwner = await anna.GetAsync<LeagueDetail>($"/leagues/{league.Id}");
        Assert.Equal(11, seenByOwner.Members.Count);
        Assert.Equal(MemberStatus.Pending, seenByOwner.Members[^1].Status);
        var email = Assert.Single(Factory.Emails.Sent, e => e.Subject == "Elva vill gå med i Kompisligan");
        Assert.Equal((await anna.GetAsync<MeResponse>("/me")).Email, email.To);
        Assert.Contains($"https://app.test/liga/{league.Id}/uppgradera", email.Text);
    }

    [Fact]
    public async Task Leaving_lets_the_first_pending_player_in()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var players = new List<HttpClient>();
        for (var i = 2; i <= 10; i++)
            players.Add(await JoinedPlayerAsync($"Spelare {i}", league));
        var first = await JoinedPlayerAsync("Först i kön", league, MemberStatus.Pending);
        var second = await JoinedPlayerAsync("Tvåa i kön", league, MemberStatus.Pending);
        var leaverId = (await players[0].GetAsync<MeResponse>("/me")).Id;

        var response = await players[0].DeleteAsync($"/leagues/{league.Id}/members/{leaverId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(MemberStatus.Active, (await first.GetAsync<LeagueDetail>($"/leagues/{league.Id}")).MyStatus);
        Assert.Equal(MemberStatus.Pending, (await second.GetAsync<LeagueDetail>($"/leagues/{league.Id}")).MyStatus);
        Assert.Equal(HttpStatusCode.NotFound, (await players[0].GetAsync($"/leagues/{league.Id}")).StatusCode);
        Assert.Single(Factory.Emails.Sent, e => e.Subject == "Du är med i Kompisligan!");
    }

    [Fact]
    public async Task Members_cannot_manage_the_league()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        var annaId = (await anna.GetAsync<MeResponse>("/me")).Id;

        Assert.Equal(HttpStatusCode.Forbidden, (await bertil.PatchJsonAsync($"/leagues/{league.Id}", new { name = "Min liga" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bertil.DeleteAsync($"/leagues/{league.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bertil.PostAsync($"/leagues/{league.Id}/invite-code", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bertil.DeleteAsync($"/leagues/{league.Id}/members/{annaId}")).StatusCode);
    }

    [Fact]
    public async Task Non_members_get_not_found()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var stranger = await Factory.NewPlayerAsync("Främling");

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/leagues/{league.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/leagues/{league.Id}/standings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/leagues/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Host_renames_removes_members_and_regenerates_code()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        var bertilId = (await bertil.GetAsync<MeResponse>("/me")).Id;

        var renamed = await (await anna.PatchJsonAsync($"/leagues/{league.Id}", new { name = "Nytt namn" })).ReadAsync<LeagueDetail>();
        var removed = await anna.DeleteAsync($"/leagues/{league.Id}/members/{bertilId}");
        var regenerated = await (await anna.PostAsync($"/leagues/{league.Id}/invite-code", null)).ReadAsync<LeagueDetail>();

        Assert.Equal("Nytt namn", renamed.Name);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.NotEqual(league.InviteCode, regenerated.InviteCode);
        var oldCode = await bertil.PostJsonAsync("/leagues/join", new { inviteCode = league.InviteCode });
        Assert.Equal(HttpStatusCode.NotFound, oldCode.StatusCode);
    }

    [Fact]
    public async Task Cadence_change_is_queued_for_next_season()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna, Cadence.Weekly);

        var queued = await (await anna.PatchJsonAsync($"/leagues/{league.Id}", new { nextSeasonCadence = Cadence.Daily })).ReadAsync<LeagueDetail>();
        Assert.Equal((Cadence.Weekly, (Cadence?)Cadence.Daily), (queued.Cadence, queued.NextSeasonCadence));

        SetDate(2027, 1, 4);
        var nextSeason = await anna.GetAsync<LeagueDetail>($"/leagues/{league.Id}");
        Assert.Equal((Cadence.Daily, (Cadence?)null), (nextSeason.Cadence, nextSeason.NextSeasonCadence));
        Assert.Contains(PeriodType.Day, nextSeason.Periods);
    }

    [Fact]
    public async Task Owner_deletes_league()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);

        Assert.Equal(HttpStatusCode.NoContent, (await anna.DeleteAsync($"/leagues/{league.Id}")).StatusCode);
        Assert.Empty(await anna.GetAsync<List<LeagueSummary>>("/leagues"));
        Assert.Equal(HttpStatusCode.NotFound, (await anna.GetAsync($"/leagues/{league.Id}")).StatusCode);
    }
}
