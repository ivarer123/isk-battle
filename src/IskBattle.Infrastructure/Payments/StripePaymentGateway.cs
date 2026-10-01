using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace IskBattle.Infrastructure.Payments;

/// <summary>
/// Stripe Checkout (card + Swish). Stripe hosts the payment page; no card data touches our servers.
/// </summary>
public sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeOptions _options = options.Value;
    private StripeClient? _client;

    public bool IsConfigured => !string.IsNullOrEmpty(_options.SecretKey) && !string.IsNullOrEmpty(_options.WebhookSecret);

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        _client ??= new StripeClient(_options.SecretKey);
        var metadata = new Dictionary<string, string>
        {
            ["purchaseId"] = request.PurchaseId.ToString(),
            ["leagueId"] = request.LeagueId.ToString(),
            ["fromTier"] = request.FromTier,
            ["toTier"] = request.ToTier,
        };

        var session = await new SessionService(_client).CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            Currency = "sek",
            // Filters the methods enabled in the Stripe Dashboard; Swish must be activated there too.
            AllowedPaymentMethodTypes = ["card", "swish"],
            Locale = "sv",
            ClientReferenceId = request.PurchaseId.ToString(),
            CustomerEmail = string.IsNullOrEmpty(request.CustomerEmail) ? null : request.CustomerEmail,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "sek",
                        UnitAmount = request.AmountOre,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"ISK Battle – upp till {request.Capacity} medlemmar",
                            Description = $"Permanent uppgradering av ligan {request.LeagueName}",
                        },
                    },
                },
            ],
            Metadata = metadata,
            // Stripe sends the receipt (GAME-DESIGN.md §9).
            PaymentIntentData = new SessionPaymentIntentDataOptions
            {
                Metadata = metadata,
                ReceiptEmail = string.IsNullOrEmpty(request.CustomerEmail) ? null : request.CustomerEmail,
            },
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
        }, cancellationToken: cancellationToken);

        return new CheckoutSession(session.Id, session.Url);
    }

    public PaymentEvent ParseWebhook(string json, string? signatureHeader)
    {
        Event stripeEvent;
        try
        {
            // Only long-stable session fields are read, so events from a newer API version are accepted.
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader ?? "", _options.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException exception)
        {
            throw new InvalidWebhookException("Invalid Stripe signature or payload.", exception);
        }

        if (stripeEvent.Data.Object is not Session session)
            return new PaymentEvent(PaymentEventKind.Ignored, null, null);

        return stripeEvent.Type switch
        {
            EventTypes.CheckoutSessionCompleted when session.PaymentStatus == "paid" =>
                new PaymentEvent(PaymentEventKind.Paid, session.Id, session.PaymentIntentId),
            EventTypes.CheckoutSessionAsyncPaymentSucceeded =>
                new PaymentEvent(PaymentEventKind.Paid, session.Id, session.PaymentIntentId),
            EventTypes.CheckoutSessionAsyncPaymentFailed or EventTypes.CheckoutSessionExpired =>
                new PaymentEvent(PaymentEventKind.Failed, session.Id, null),
            // e.g. checkout.session.completed with an async (pending) payment: wait for async_payment_succeeded.
            _ => new PaymentEvent(PaymentEventKind.Ignored, session.Id, null),
        };
    }
}
