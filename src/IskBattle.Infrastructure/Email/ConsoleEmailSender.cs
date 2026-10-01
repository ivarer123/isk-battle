using Microsoft.Extensions.Logging;

namespace IskBattle.Infrastructure.Email;

/// <summary>Development: logs rendered emails instead of sending them.</summary>
internal sealed partial class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        LogEmail(message.To, message.Subject, message.Text, message.UnsubscribeUrl);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Email to {To}: {Subject}\n{Text}\nUnsubscribe: {UnsubscribeUrl}")]
    private partial void LogEmail(string to, string subject, string text, string unsubscribeUrl);
}
