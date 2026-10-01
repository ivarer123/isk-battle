using System.Net;
using System.Text;
using IskBattle.Api.Tests.Api;
using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Tests.Payments;

public class CheckoutTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    private async Task<string> CheckoutAsync(HttpClient player, Guid leagueId, LeagueTier tier = LeagueTier.Premium50)
    {
        var response = await player.PostJsonAsync($"/leagues/{leagueId}/checkout",
            new { targetTier = tier, immediateDeliveryConsent = true });
        return FakePaymentGateway.SessionIdFromUrl((await response.ReadAsync<CheckoutResponse>()).CheckoutUrl);
    }

    private Task<HttpResponseMessage> WebhookAsync(string kind, string sessionId, string signature = FakePaymentGateway.ValidSignature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent($$"""{"kind":"{{kind}}","sessionId":"{{sessionId}}"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", signature);
        return Factory.CreateClient().SendAsync(request);
    }

    private async Task<(LeagueDetail League, HttpClient Owner, List<HttpClient> Pending)> FullLeagueAsync(int pending)
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);
        for (var i = 2; i <= 10; i++)
            await JoinedPlayerAsync($"Spelare {i}", league);
        var queue = new List<HttpClient>();
        for (var i = 0; i < pending; i++)
            queue.Add(await JoinedPlayerAsync($"Kö {i + 1}", league, MemberStatus.Pending));
        return (league, owner, queue);
    }

    private async Task<PurchaseStatus> PurchaseStatusAsync(string sessionId)
    {
        await using var db = Sql.CreateDbContext();
        return (await db.Purchases.SingleAsync(p => p.StripeCheckoutSessionId == sessionId, TestContext.Current.CancellationToken)).Status;
    }

    [Fact]
    public async Task Checkout_charges_the_difference_and_records_consent()
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);

        var sessionId = await CheckoutAsync(owner, league.Id, LeagueTier.Premium100);

        var request = Assert.Single(Factory.Payments.Requests, r => r.LeagueId == league.Id);
        Assert.Equal((19_900, "Free", "Premium100", 100), (request.AmountOre, request.FromTier, request.ToTier, request.Capacity));
        Assert.Equal($"https://app.test/liga/{league.Id}?betalning=klar", request.SuccessUrl);
        Assert.EndsWith("@example.com", request.CustomerEmail);
        await using var db = Sql.CreateDbContext();
        var purchase = await db.Purchases.SingleAsync(p => p.StripeCheckoutSessionId == sessionId, TestContext.Current.CancellationToken);
        Assert.Equal((PurchaseStatus.Created, 19_900), (purchase.Status, purchase.AmountOre));
        Assert.Equal(Factory.Time.GetUtcNow().Date, purchase.ImmediateDeliveryConsentAt.Date);
    }

    [Theory]
    [InlineData(LeagueTier.Premium50, false, "consent_required")]
    [InlineData(LeagueTier.Free, true, "tier_invalid")]
    public async Task Invalid_checkouts_are_rejected(LeagueTier tier, bool consent, string code)
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);

        var response = await owner.PostJsonAsync($"/leagues/{league.Id}/checkout", new { targetTier = tier, immediateDeliveryConsent = consent });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task Checkout_without_stripe_configuration_is_unavailable()
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);
        Factory.Payments.IsConfigured = false;

        var response = await owner.PostJsonAsync($"/leagues/{league.Id}/checkout", new { targetTier = LeagueTier.Premium50, immediateDeliveryConsent = true });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Paid_webhook_upgrades_promotes_buyer_and_lets_the_queue_in()
    {
        var (league, _, pending) = await FullLeagueAsync(pending: 2);
        var buyer = await JoinedPlayerAsync("Köparen", league, MemberStatus.Pending); // third in the queue pays
        var sessionId = await CheckoutAsync(buyer, league.Id);

        var response = await WebhookAsync("Paid", sessionId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PurchaseStatus.Paid, await PurchaseStatusAsync(sessionId));
        var seenByBuyer = await buyer.GetAsync<LeagueDetail>($"/leagues/{league.Id}");
        Assert.Equal((LeagueTier.Premium50, 50, 13, MemberRole.CoHost, MemberStatus.Active),
            (seenByBuyer.Tier, seenByBuyer.Capacity, seenByBuyer.ActiveMemberCount, seenByBuyer.MyRole, seenByBuyer.MyStatus));
        Assert.NotNull(seenByBuyer.InviteCode); // co-hosts manage the league
        Assert.Equal(MemberStatus.Active, (await pending[0].GetAsync<LeagueDetail>($"/leagues/{league.Id}")).MyStatus);
        Assert.Equal(3, Factory.Emails.Sent.Count(e => e.Subject == "Du är med i Kompisligan!"));
    }

    [Fact]
    public async Task Replayed_webhook_is_idempotent()
    {
        var (league, owner, _) = await FullLeagueAsync(pending: 1);
        var sessionId = await CheckoutAsync(owner, league.Id);

        await WebhookAsync("Paid", sessionId);
        var replay = await WebhookAsync("Paid", sessionId);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Single(Factory.Emails.Sent, e => e.Subject == "Du är med i Kompisligan!");
        Assert.Equal(MemberRole.Owner, (await owner.GetAsync<LeagueDetail>($"/leagues/{league.Id}")).MyRole);
    }

    [Fact]
    public async Task Second_buyer_of_the_same_tier_needs_a_refund()
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        var first = await CheckoutAsync(owner, league.Id);
        var second = await CheckoutAsync(bertil, league.Id);

        await WebhookAsync("Paid", first);
        await WebhookAsync("Paid", second);

        Assert.Equal((PurchaseStatus.Paid, PurchaseStatus.NeedsRefund), (await PurchaseStatusAsync(first), await PurchaseStatusAsync(second)));
        Assert.Equal(MemberRole.Member, (await bertil.GetAsync<LeagueDetail>($"/leagues/{league.Id}")).MyRole);
    }

    [Fact]
    public async Task Concurrent_paid_webhooks_upgrade_once()
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);
        var bertil = await JoinedPlayerAsync("Bertil", league);
        var first = await CheckoutAsync(owner, league.Id);
        var second = await CheckoutAsync(bertil, league.Id);

        var responses = await Task.WhenAll(WebhookAsync("Paid", first), WebhookAsync("Paid", second));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var statuses = new[] { await PurchaseStatusAsync(first), await PurchaseStatusAsync(second) };
        Assert.Equal([PurchaseStatus.Paid, PurchaseStatus.NeedsRefund], statuses.Order());
    }

    [Fact]
    public async Task Expired_checkout_fails_the_purchase()
    {
        var owner = await Factory.NewPlayerAsync("Anna");
        var league = await CreateLeagueAsync(owner);
        var sessionId = await CheckoutAsync(owner, league.Id);

        await WebhookAsync("Failed", sessionId);

        Assert.Equal(PurchaseStatus.Failed, await PurchaseStatusAsync(sessionId));
        Assert.Equal(LeagueTier.Free, (await owner.GetAsync<LeagueDetail>($"/leagues/{league.Id}")).Tier);
    }

    [Fact]
    public async Task Unsigned_webhooks_are_rejected()
    {
        var response = await WebhookAsync("Paid", "cs_test_anything", signature: "forged");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_sessions_and_ignored_events_are_acknowledged()
    {
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync("Paid", "cs_test_unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await WebhookAsync("Ignored", "cs_test_unknown")).StatusCode);
    }
}
