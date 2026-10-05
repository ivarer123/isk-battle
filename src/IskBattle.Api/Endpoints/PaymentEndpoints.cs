using IskBattle.Api.Auth;
using IskBattle.Api.Leagues;
using IskBattle.Domain;
using IskBattle.Domain.Leagues;
using IskBattle.Domain.Purchases;
using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Notifications;
using IskBattle.Infrastructure.Payments;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Endpoints;

/// <summary>Stripe Checkout flow (ARCHITECTURE.md §7).</summary>
internal static partial class PaymentEndpoints
{
    private const int MaxConcurrencyRetries = 3;
    private const string LogCategory = "IskBattle.Api.Payments";

    public static void MapCheckoutEndpoint(this RouteGroupBuilder api) =>
        api.MapPost("/leagues/{id:guid}/checkout", Checkout)
            .RequireLeague(LeagueAccessLevel.AnyMember) // Pending members may pay to get in
            .Produces<CheckoutResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    public static void MapStripeWebhook(this IEndpointRouteBuilder app) =>
        app.MapPost("/webhooks/stripe", Webhook).AllowAnonymous().ExcludeFromDescription();

    private static async Task<IResult> Checkout(
        Guid id,
        StartCheckoutRequest request,
        HttpContext http,
        IskBattleDbContext db,
        IPaymentGateway gateway,
        IOptions<AppLinkOptions> links,
        TimeProvider time,
        ILoggerFactory loggers)
    {
        var logger = loggers.CreateLogger(LogCategory);
        if (!gateway.IsConfigured)
            return Problems.Of(StatusCodes.Status503ServiceUnavailable, "payments_unavailable", "Payments are not available right now.");

        var (league, _) = http.GetLeague();
        switch (PurchaseRules.ValidateCheckout(league, request.TargetTier, request.ImmediateDeliveryConsent))
        {
            case CheckoutError.ConsentRequired:
                return Problems.BadRequest("consent_required", "Immediate delivery consent is required.");
            case CheckoutError.TierNotHigher:
                return Problems.BadRequest("tier_invalid", "Choose a tier above the current one.");
        }

        var user = http.GetCurrentUser();
        var purchase = PurchaseRules.Create(league, user.Id, request.TargetTier, time.GetUtcNow());
        var leagueUrl = $"{links.Value.AppBaseUrl.TrimEnd('/')}/liga/{league.Id}";
        CheckoutSession session;
        try
        {
            session = await gateway.CreateCheckoutAsync(new CheckoutRequest(
                purchase.Id,
                league.Id,
                league.Name,
                purchase.FromTier.ToString(),
                purchase.ToTier.ToString(),
                GameConfig.Tiers[purchase.ToTier].Capacity,
                purchase.AmountOre,
                user.Email,
                SuccessUrl: $"{leagueUrl}?betalning=klar",
                CancelUrl: $"{leagueUrl}/uppgradera?betalning=avbruten"), http.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCheckoutFailed(logger, exception, league.Id);
            return Problems.Of(StatusCodes.Status502BadGateway, "payments_failed", "Could not start the payment. Try again.");
        }

        purchase.StripeCheckoutSessionId = session.SessionId;
        db.Purchases.Add(purchase);
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.Ok(new CheckoutResponse(session.Url));
    }

    /// <summary>Signature-verified and idempotent. Unknown sessions and event types are acknowledged with 200.</summary>
    private static async Task<IResult> Webhook(
        HttpRequest request,
        IskBattleDbContext db,
        IPaymentGateway gateway,
        INotificationService notifications,
        TimeProvider time,
        ILoggerFactory loggers)
    {
        var logger = loggers.CreateLogger(LogCategory);
        var json = await new StreamReader(request.Body).ReadToEndAsync(request.HttpContext.RequestAborted);
        PaymentEvent paymentEvent;
        try
        {
            paymentEvent = gateway.ParseWebhook(json, request.Headers["Stripe-Signature"]);
        }
        catch (InvalidWebhookException)
        {
            return Results.BadRequest();
        }

        if (paymentEvent.Kind == PaymentEventKind.Ignored || paymentEvent.SessionId is null)
            return Results.Ok();

        var ct = request.HttpContext.RequestAborted;
        for (var attempt = 1; ; attempt++)
        {
            var purchase = await db.Purchases.SingleOrDefaultAsync(p => p.StripeCheckoutSessionId == paymentEvent.SessionId, ct);
            if (purchase is null)
            {
                LogUnknownSession(logger, paymentEvent.SessionId);
                return Results.Ok();
            }

            if (paymentEvent.Kind == PaymentEventKind.Failed)
            {
                PurchaseRules.MarkFailed(purchase, time.GetUtcNow());
                await db.SaveChangesAsync(ct);
                return Results.Ok();
            }

            if (purchase.Status is not (PurchaseStatus.Created or PurchaseStatus.Failed))
                return Results.Ok(); // already completed

            var league = await db.Leagues.IgnoreQueryFilters().Include(l => l.Members).SingleAsync(l => l.Id == purchase.LeagueId, ct);
            var completion = PurchaseRules.CompletePaid(league, purchase, time.GetUtcNow());
            purchase.StripePaymentIntentId = paymentEvent.PaymentIntentId;
            if (completion.Status is PurchaseStatus.NeedsRefund or PurchaseStatus.PaidNeedsPartialRefund)
                LogNeedsRefund(logger, purchase.Id, league.Id, purchase.RefundDueOre);

            // Tier, roles, activations and the purchase are saved together; the league rowversion guards
            // against a concurrent upgrade or join.
            if (await db.TrySaveLeagueAsync(league, ct))
            {
                if (completion.Activated.Count > 0)
                    await notifications.MembersActivatedAsync(league, [.. completion.Activated.Select(m => m.UserId)], ct);
                return Results.Ok();
            }

            if (attempt == MaxConcurrencyRetries)
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); // Stripe retries later
            db.ChangeTracker.Clear();
        }
    }

    [LoggerMessage(LogLevel.Error, "Could not create Stripe checkout for league {LeagueId}")]
    private static partial void LogCheckoutFailed(ILogger logger, Exception exception, Guid leagueId);

    [LoggerMessage(LogLevel.Warning, "Stripe webhook for unknown checkout session {SessionId}")]
    private static partial void LogUnknownSession(ILogger logger, string sessionId);

    [LoggerMessage(LogLevel.Warning, "Purchase {PurchaseId} for league {LeagueId} needs a manual refund of {RefundDueOre} öre in Stripe")]
    private static partial void LogNeedsRefund(ILogger logger, Guid purchaseId, Guid leagueId, int refundDueOre);
}
