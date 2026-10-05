using System.Net;
using IskBattle.Api.Tests.Api;
using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Tests.Email;

public class UnsubscribeTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private static readonly UnsubscribeTokens Tokens =
        new(Options.Create(new EmailOptions { UnsubscribeKey = ApiFactory.TestUnsubscribeKey }));

    [Fact]
    public void Tokens_round_trip_and_reject_tampering()
    {
        var userId = Guid.NewGuid();
        var token = Tokens.Create(userId);
        var other = new UnsubscribeTokens(Options.Create(new EmailOptions { UnsubscribeKey = new string('x', 40) }));

        Assert.Equal(userId, Tokens.Validate(token));
        Assert.Null(Tokens.Validate(Tokens.Create(Guid.NewGuid()).Split('.')[0] + "." + token.Split('.')[1]));
        Assert.Null(other.Validate(token));
        Assert.Null(Tokens.Validate("not-a-token"));
        Assert.Null(Tokens.Validate("!!.??"));
        Assert.Null(Tokens.Validate(null));
    }

    [Fact]
    public void Short_keys_are_refused() =>
        Assert.Throws<InvalidOperationException>(() => new UnsubscribeTokens(Options.Create(new EmailOptions { UnsubscribeKey = "short" })));

    [Fact]
    public async Task Opening_the_link_only_asks_for_confirmation()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var me = await anna.GetAsync<MeResponse>("/me");
        var token = Uri.EscapeDataString(Tokens.Create(me.Id));

        var response = await Factory.CreateClient().GetAsync($"/unsubscribe?token={token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form method=\"post\"", html);
        Assert.Contains($"?token={token}", html.Replace("&#x3D;", "="));
        Assert.True((await anna.GetAsync<MeResponse>("/me")).EmailRemindersEnabled);
    }

    [Fact]
    public async Task Posting_turns_off_email_without_signing_in()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var me = await anna.GetAsync<MeResponse>("/me");
        var url = $"/unsubscribe?token={Uri.EscapeDataString(Tokens.Create(me.Id))}";

        // RFC 8058 one-click body, as sent by mail clients.
        var response = await Factory.CreateClient().PostAsync(url,
            new FormUrlEncodedContent([new KeyValuePair<string, string>("List-Unsubscribe", "One-Click")]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Du får inga fler mejl", await response.Content.ReadAsStringAsync());
        Assert.False((await anna.GetAsync<MeResponse>("/me")).EmailRemindersEnabled);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task Invalid_link_is_rejected(string method)
    {
        var response = await Factory.CreateClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), "/unsubscribe?token=forged.token"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Hosts_who_unsubscribed_get_no_join_emails()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        await anna.PatchJsonAsync("/me", new { emailRemindersEnabled = false });
        for (var i = 2; i <= 10; i++)
            await JoinedPlayerAsync($"Spelare {i}", league);

        await JoinedPlayerAsync("Elva", league, Domain.MemberStatus.Pending);

        Assert.DoesNotContain(Factory.Emails.Sent, e => e.Subject.Contains("vill gå med"));
    }

    [Fact]
    public async Task Email_failures_do_not_break_joining()
    {
        var anna = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(anna);
        for (var i = 2; i <= 10; i++)
            await JoinedPlayerAsync($"Spelare {i}", league);
        Factory.Emails.Fail = true;

        await JoinedPlayerAsync("Elva", league, Domain.MemberStatus.Pending);
    }
}
