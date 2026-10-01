using System.Text.Json;
using IskBattle.Infrastructure.Payments;

namespace IskBattle.Api.Tests.Infrastructure;

/// <summary>Stands in for Stripe. Webhooks are JSON <c>{ kind, sessionId }</c> signed with "valid".</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public const string ValidSignature = "valid";

    private readonly List<CheckoutRequest> _requests = [];

    public bool IsConfigured { get; set; } = true;

    public IReadOnlyList<CheckoutRequest> Requests
    {
        get { lock (_requests) return [.. _requests]; }
    }

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        lock (_requests) _requests.Add(request);
        var sessionId = $"cs_test_{request.PurchaseId:N}";
        return Task.FromResult(new CheckoutSession(sessionId, $"https://checkout.stripe.test/{sessionId}"));
    }

    public PaymentEvent ParseWebhook(string json, string? signatureHeader)
    {
        if (signatureHeader != ValidSignature)
            throw new InvalidWebhookException("Bad signature.");
        var body = JsonDocument.Parse(json).RootElement;
        return new PaymentEvent(
            Enum.Parse<PaymentEventKind>(body.GetProperty("kind").GetString()!),
            body.GetProperty("sessionId").GetString(),
            "pi_test_123");
    }

    public static string SessionIdFromUrl(string checkoutUrl) => checkoutUrl[(checkoutUrl.LastIndexOf('/') + 1)..];
}
