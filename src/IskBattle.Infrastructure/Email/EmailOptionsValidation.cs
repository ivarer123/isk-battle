using Microsoft.Extensions.Options;

namespace IskBattle.Infrastructure.Email;

/// <summary>
/// Checked at startup (ValidateOnStart) so a missing Key Vault secret fails the deployment instead of
/// returning 500 from every endpoint that sends email, including the Stripe webhook.
/// </summary>
internal sealed class EmailOptionsValidation : IValidateOptions<EmailOptions>, IValidateOptions<AppLinkOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        List<string> errors = [];
        if (options.UnsubscribeKey.Length < UnsubscribeTokens.MinKeyLength)
            errors.Add($"Email:UnsubscribeKey must be at least {UnsubscribeTokens.MinKeyLength} characters.");
        if (string.Equals(options.Provider, "Acs", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.SenderAddress))
                errors.Add("Email:SenderAddress is required for the Acs provider.");
            if (string.IsNullOrWhiteSpace(options.AcsEndpoint) && string.IsNullOrWhiteSpace(options.AcsConnectionString))
                errors.Add("Email:AcsEndpoint or Email:AcsConnectionString is required for the Acs provider.");
            else if (string.IsNullOrWhiteSpace(options.AcsConnectionString) && !IsAbsoluteHttpUrl(options.AcsEndpoint))
                errors.Add("Email:AcsEndpoint must be an absolute URL.");
        }
        else if (!string.Equals(options.Provider, "Console", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Email:Provider must be Acs or Console.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    public ValidateOptionsResult Validate(string? name, AppLinkOptions options)
    {
        List<string> errors = [];
        if (!IsAbsoluteHttpUrl(options.AppBaseUrl))
            errors.Add("Links:AppBaseUrl must be an absolute http(s) URL.");
        if (!IsAbsoluteHttpUrl(options.ApiBaseUrl))
            errors.Add("Links:ApiBaseUrl must be an absolute http(s) URL.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http";
}
