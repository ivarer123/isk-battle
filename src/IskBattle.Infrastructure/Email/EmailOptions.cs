namespace IskBattle.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>"Console" (development: log rendered emails) or "Acs" (Azure Communication Services).</summary>
    public string Provider { get; set; } = "Console";

    /// <summary>Verified sender, e.g. "ISK Battle &lt;noreply@example.se&gt;" address part only.</summary>
    public string SenderAddress { get; set; } = "";

    /// <summary>ACS endpoint for managed identity, e.g. https://acs-isk-battle.europe.communication.azure.com.</summary>
    public string? AcsEndpoint { get; set; }

    /// <summary>Alternative to the endpoint for local testing against ACS.</summary>
    public string? AcsConnectionString { get; set; }

    /// <summary>HMAC key for unsubscribe tokens (Key Vault secret). At least 32 bytes.</summary>
    public string UnsubscribeKey { get; set; } = "";
}

public sealed class AppLinkOptions
{
    public const string Section = "Links";

    /// <summary>Public web app origin, e.g. https://app.example.se.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Public API origin used for unsubscribe links, e.g. https://api.example.se.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";
}
