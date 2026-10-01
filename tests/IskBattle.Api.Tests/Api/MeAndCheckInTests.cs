using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;

namespace IskBattle.Api.Tests.Api;

public class MeAndCheckInTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    [Fact]
    public async Task Requests_without_a_token_are_rejected()
    {
        var response = await Factory.CreateClient().GetAsync("/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_request_creates_the_user_and_onboarding_sets_the_name()
    {
        var player = await Factory.NewPlayerAsync("  Anna  ");

        var me = await player.GetAsync<MeResponse>("/me");

        Assert.Equal(("Anna", true, (Cadence?)null, new DateOnly(2026, 9, 21), (CheckInResponse?)null),
            (me.DisplayName, me.EmailRemindersEnabled, me.EffectiveCadence, me.Today, me.LastCheckIn));
        Assert.EndsWith("@example.com", me.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("This display name is far too long to be shown in a standings table")]
    public async Task Invalid_display_names_are_rejected(string name)
    {
        var player = await Factory.NewPlayerAsync("Anna");

        var response = await player.PatchJsonAsync("/me", new { displayName = name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("display_name_invalid", await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Reminders_can_be_turned_off()
    {
        var player = await Factory.NewPlayerAsync("Anna");

        var me = await (await player.PatchJsonAsync("/me", new { emailRemindersEnabled = false })).ReadAsync<MeResponse>();

        Assert.False(me.EmailRemindersEnabled);
        Assert.Equal("Anna", me.DisplayName);
    }

    [Fact]
    public async Task Effective_cadence_is_the_most_frequent_of_active_leagues()
    {
        var player = await Factory.NewPlayerAsync("Anna");
        await CreateLeagueAsync(player, Cadence.Monthly);
        await CreateLeagueAsync(player, Cadence.Weekly);

        Assert.Equal(Cadence.Weekly, (await player.GetAsync<MeResponse>("/me")).EffectiveCadence);
    }

    [Fact]
    public async Task Check_in_today_and_overwrite_it()
    {
        var player = await Factory.NewPlayerAsync("Anna");

        var first = await (await player.PostJsonAsync("/check-ins", new { ytdPercent = 12.4m })).ReadAsync<CheckInResponse>();
        var second = await (await player.PostJsonAsync("/check-ins", new { ytdPercent = 12.5m })).ReadAsync<CheckInResponse>();

        Assert.Equal(new CheckInResponse(new DateOnly(2026, 9, 21), 12.4m), first);
        Assert.Equal(new CheckInResponse(new DateOnly(2026, 9, 21), 12.5m), second);
        Assert.Equal([second], await player.GetAsync<List<CheckInResponse>>("/me/check-ins?season=2026"));
        Assert.Equal(second, (await player.GetAsync<MeResponse>("/me")).LastCheckIn);
    }

    [Fact]
    public async Task Big_jump_requires_confirmation()
    {
        var player = await Factory.NewPlayerAsync("Anna");
        SetDate(2026, 9, 18);
        await CheckInAsync(player, 10m);
        SetDate(2026, 9, 21);

        var response = await player.PostJsonAsync("/check-ins", new { ytdPercent = 100m, confirmed = false });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("requiresConfirmation").GetBoolean());
        Assert.Equal(10m, problem.GetProperty("previousYtdPercent").GetDecimal());

        var confirmed = await player.PostJsonAsync("/check-ins", new { ytdPercent = 100m, confirmed = true });
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Theory]
    [InlineData(-100, "ytd_out_of_range")]
    [InlineData(1000.01, "ytd_out_of_range")]
    [InlineData(1.23456, "ytd_too_precise")]
    public async Task Invalid_values_are_rejected(double ytd, string code)
    {
        var player = await Factory.NewPlayerAsync("Anna");

        var response = await player.PostJsonAsync("/check-ins", new { ytdPercent = (decimal)ytd, confirmed = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task History_is_per_season_and_past_days_are_kept()
    {
        var player = await Factory.NewPlayerAsync("Anna");
        SetDate(2025, 12, 31);
        await CheckInAsync(player, 30m);
        SetDate(2026, 1, 2);
        await CheckInAsync(player, 1m);
        SetDate(2026, 1, 5);
        await CheckInAsync(player, 2m);

        var history = await player.GetAsync<List<CheckInResponse>>("/me/check-ins?season=2026");

        Assert.Equal([new(new DateOnly(2026, 1, 2), 1m), new(new DateOnly(2026, 1, 5), 2m)], history);
    }

    [Fact]
    public async Task Check_ins_are_rate_limited()
    {
        var player = await Factory.NewPlayerAsync("Anna");

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
            statuses.Add((await player.PostJsonAsync("/check-ins", new { ytdPercent = 1m })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.All(statuses.Take(10), s => Assert.Equal(HttpStatusCode.OK, s));
    }

    [Fact]
    public async Task Deleting_the_account_hands_over_the_league()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        await CheckInAsync(anna, 5m);

        var response = await anna.DeleteAsync("/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var detail = await bertil.GetAsync<LeagueDetail>($"/leagues/{league.Id}");
        Assert.Equal((MemberRole.Owner, 1), (detail.MyRole, detail.ActiveMemberCount));
    }
}
