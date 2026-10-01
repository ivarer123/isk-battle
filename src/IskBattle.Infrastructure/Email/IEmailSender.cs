namespace IskBattle.Infrastructure.Email;

/// <param name="UnsubscribeUrl">One-click unsubscribe (also sent as List-Unsubscribe headers).</param>
public sealed record EmailMessage(string To, string Subject, string Html, string Text, string UnsubscribeUrl);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
