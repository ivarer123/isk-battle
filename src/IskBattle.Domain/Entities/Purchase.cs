namespace IskBattle.Domain.Entities;

public class Purchase
{
    public Guid Id { get; set; }
    public Guid LeagueId { get; set; }
    public Guid? BuyerUserId { get; set; }          // nulled on account deletion
    public LeagueTier FromTier { get; set; }
    public LeagueTier ToTier { get; set; }
    public int AmountOre { get; set; }              // money is always integer öre
    public int RefundDueOre { get; set; }           // to refund manually in Stripe (NeedsRefund / PaidNeedsPartialRefund)
    public string StripeCheckoutSessionId { get; set; } = "";  // unique
    public string? StripePaymentIntentId { get; set; }
    public PurchaseStatus Status { get; set; }
    public DateTimeOffset ImmediateDeliveryConsentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
