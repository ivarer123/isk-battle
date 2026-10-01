using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace IskBattle.Infrastructure.Email;

/// <summary>Azure Communication Services Email. Uses managed identity when an endpoint is configured.</summary>
internal sealed class AcsEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailClient _client = Create(options.Value);
    private readonly string _sender = options.Value.SenderAddress;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var email = new Azure.Communication.Email.EmailMessage(
            _sender,
            message.To,
            new EmailContent(message.Subject) { Html = message.Html, PlainText = message.Text });
        // RFC 8058 one-click unsubscribe.
        email.Headers.Add("List-Unsubscribe", $"<{message.UnsubscribeUrl}>");
        email.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");

        await _client.SendAsync(WaitUntil.Started, email, cancellationToken);
    }

    private static EmailClient Create(EmailOptions options) =>
        !string.IsNullOrEmpty(options.AcsConnectionString)
            ? new EmailClient(options.AcsConnectionString)
            : new EmailClient(new Uri(options.AcsEndpoint ?? throw new InvalidOperationException("Email:AcsEndpoint is not configured.")),
                new DefaultAzureCredential());
}
