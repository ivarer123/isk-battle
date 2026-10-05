using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;

namespace IskBattle.Domain.Purchases;

public enum CheckoutError { None, ConsentRequired, TierNotHigher }

public sealed record PurchaseCompletion(PurchaseStatus Status, IReadOnlyList<LeagueMember> Activated);

/// <summary>Upgrade purchases (GAME-DESIGN.md §8, ARCHITECTURE.md §7).</summary>
public static class PurchaseRules
{
    public static CheckoutError ValidateCheckout(League league, LeagueTier target, bool immediateDeliveryConsent) =>
        !immediateDeliveryConsent ? CheckoutError.ConsentRequired
        : target <= league.Tier || !Enum.IsDefined(target) ? CheckoutError.TierNotHigher
        : CheckoutError.None;

    /// <summary>Creates a purchase for the difference between the league's current tier and <paramref name="target"/>.</summary>
    public static Purchase Create(League league, Guid buyerUserId, LeagueTier target, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        LeagueId = league.Id,
        BuyerUserId = buyerUserId,
        FromTier = league.Tier,
        ToTier = target,
        AmountOre = GameConfig.UpgradePriceOre(league.Tier, target),
        Status = PurchaseStatus.Created,
        ImmediateDeliveryConsentAt = now,
        CreatedAt = now,
    };

    /// <summary>
    /// Applies a paid purchase: tier becomes max(current, ToTier), the buyer becomes co-host (activated first
    /// if Pending), and Pending members are activated first-come, first-served. If the league already has
    /// the tier (two concurrent buyers) or was deleted, nothing changes and the purchase needs a refund. If
    /// the league moved up to a tier between FromTier and ToTier after checkout, the upgrade still applies
    /// and the price difference is due as a partial refund.
    /// </summary>
    public static PurchaseCompletion CompletePaid(League league, Purchase purchase, DateTimeOffset now)
    {
        if (purchase.Status is not (PurchaseStatus.Created or PurchaseStatus.Failed))
            return new PurchaseCompletion(purchase.Status, []); // already completed (webhook redelivery)

        purchase.CompletedAt = now;
        if (league.DeletedAt is not null || league.Tier >= purchase.ToTier)
        {
            purchase.Status = PurchaseStatus.NeedsRefund;
            purchase.RefundDueOre = purchase.AmountOre;
            return new PurchaseCompletion(PurchaseStatus.NeedsRefund, []);
        }

        purchase.RefundDueOre = Math.Max(0, purchase.AmountOre - GameConfig.UpgradePriceOre(league.Tier, purchase.ToTier));
        league.Tier = purchase.ToTier;
        var buyer = purchase.BuyerUserId is { } buyerId ? league.Member(buyerId) : null;
        var wasPending = buyer?.Status == MemberStatus.Pending;
        if (buyer is not null)
            MembershipService.PromoteToCoHost(league, buyer.UserId, now);

        var activated = MembershipService.ActivatePending(league).ToList();
        if (wasPending)
            activated.Insert(0, buyer!);

        purchase.Status = purchase.RefundDueOre > 0 ? PurchaseStatus.PaidNeedsPartialRefund : PurchaseStatus.Paid;
        return new PurchaseCompletion(purchase.Status, activated);
    }

    /// <summary>Expired or failed checkouts. A purchase that already completed is left alone.</summary>
    public static void MarkFailed(Purchase purchase, DateTimeOffset now)
    {
        if (purchase.Status != PurchaseStatus.Created)
            return;
        purchase.Status = PurchaseStatus.Failed;
        purchase.CompletedAt = now;
    }
}
