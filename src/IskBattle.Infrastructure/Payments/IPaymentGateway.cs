namespace IskBattle.Infrastructure.Payments;

public sealed record CheckoutRequest(
    Guid PurchaseId,
    Guid LeagueId,
    string LeagueName,
    string FromTier,
    string ToTier,
    int Capacity,
    int AmountOre,
    string? CustomerEmail,
    string SuccessUrl,
    string CancelUrl);

public sealed record CheckoutSession(string SessionId, string Url);

public enum PaymentEventKind { Paid, Failed, Ignored }

/// <param name="PaymentIntentId">Set for paid sessions.</param>
public sealed record PaymentEvent(PaymentEventKind Kind, string? SessionId, string? PaymentIntentId);

public interface IPaymentGateway
{
    /// <summary>False when payments are not configured (e.g. local development without Stripe keys).</summary>
    bool IsConfigured { get; }

    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>Verifies the webhook signature and classifies the event. Throws <see cref="InvalidWebhookException"/> if invalid.</summary>
    PaymentEvent ParseWebhook(string json, string? signatureHeader);
}

public sealed class InvalidWebhookException(string message, Exception? inner = null) : Exception(message, inner);
