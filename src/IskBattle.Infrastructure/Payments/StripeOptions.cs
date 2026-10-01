namespace IskBattle.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string Section = "Stripe";

    /// <summary>sk_test_… locally and in test mode; sk_live_… at launch (Key Vault secret).</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>whsec_… signing secret of the webhook endpoint (Key Vault secret).</summary>
    public string WebhookSecret { get; set; } = "";
}
