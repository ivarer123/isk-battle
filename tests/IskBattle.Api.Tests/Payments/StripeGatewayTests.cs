using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IskBattle.Infrastructure.Payments;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Tests.Payments;

/// <summary>Exercises the real Stripe signature verification and event mapping with locally signed payloads.</summary>
public class StripeGatewayTests
{
    private const string Secret = "whsec_test_secret";

    private static readonly StripePaymentGateway Gateway =
        new(Options.Create(new StripeOptions { SecretKey = "sk_test_x", WebhookSecret = Secret }));

    private static string Event(string type, string paymentStatus) => $$"""
        {
          "id": "evt_test_1",
          "object": "event",
          "api_version": "2020-08-27",
          "created": 1790000000,
          "livemode": false,
          "type": "{{type}}",
          "data": {
            "object": {
              "id": "cs_test_abc",
              "object": "checkout.session",
              "payment_status": "{{paymentStatus}}",
              "payment_intent": "pi_test_123"
            }
          }
        }
        """;

    private static string Sign(string payload, string secret = Secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}")));
        return $"t={timestamp},v1={signature}";
    }

    [Theory]
    [InlineData("checkout.session.completed", "paid", PaymentEventKind.Paid)]
    [InlineData("checkout.session.completed", "unpaid", PaymentEventKind.Ignored)] // async Swish still pending
    [InlineData("checkout.session.async_payment_succeeded", "paid", PaymentEventKind.Paid)]
    [InlineData("checkout.session.async_payment_failed", "unpaid", PaymentEventKind.Failed)]
    [InlineData("checkout.session.expired", "unpaid", PaymentEventKind.Failed)]
    public void Maps_checkout_events(string type, string paymentStatus, PaymentEventKind expected)
    {
        var payload = Event(type, paymentStatus);

        var parsed = Gateway.ParseWebhook(payload, Sign(payload));

        Assert.Equal((expected, "cs_test_abc"), (parsed.Kind, parsed.SessionId));
    }

    [Fact]
    public void Paid_event_carries_the_payment_intent()
    {
        var payload = Event("checkout.session.completed", "paid");

        Assert.Equal("pi_test_123", Gateway.ParseWebhook(payload, Sign(payload)).PaymentIntentId);
    }

    [Theory]
    [InlineData("whsec_other")]
    [InlineData(null)]
    public void Rejects_bad_signatures(string? signingSecret)
    {
        var payload = Event("checkout.session.completed", "paid");
        var header = signingSecret is null ? null : Sign(payload, signingSecret);

        Assert.Throws<InvalidWebhookException>(() => Gateway.ParseWebhook(payload, header));
    }

    [Fact]
    public void Rejects_tampered_payloads()
    {
        var payload = Event("checkout.session.completed", "unpaid");
        var header = Sign(payload);

        Assert.Throws<InvalidWebhookException>(() => Gateway.ParseWebhook(payload.Replace("unpaid", "paid"), header));
    }

    [Fact]
    public void Configuration_requires_both_keys()
    {
        Assert.True(Gateway.IsConfigured);
        Assert.False(new StripePaymentGateway(Options.Create(new StripeOptions { SecretKey = "sk_test_x" })).IsConfigured);
    }
}
