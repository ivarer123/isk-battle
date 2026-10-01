using IskBattle.Domain;
using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;
using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IskBattle.Infrastructure.Notifications;

/// <summary>
/// League emails. Respects the user's email setting (one switch for all ISK Battle email, which the
/// unsubscribe link turns off). Delivery failures are logged, never thrown.
/// </summary>
internal sealed partial class EmailNotificationService(
    IskBattleDbContext db,
    IEmailSender sender,
    EmailLinks links,
    ILogger<EmailNotificationService> logger) : INotificationService
{
    public async Task JoinPendingAsync(League league, User joiner, CancellationToken cancellationToken = default)
    {
        var hostIds = league.Members.Where(m => m.IsHost()).Select(m => m.UserId).ToList();
        foreach (var host in await RecipientsAsync(hostIds, cancellationToken))
        {
            await SendAsync(EmailTemplates.JoinPending(
                host.Email, joiner.DisplayName, league.Name, links.Upgrade(league.Id), links.Unsubscribe(host.Id)), cancellationToken);
        }
    }

    public async Task MembersActivatedAsync(League league, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        foreach (var user in await RecipientsAsync(userIds, cancellationToken))
        {
            await SendAsync(EmailTemplates.MemberActivated(
                user.Email, user.DisplayName, league.Name, links.League(league.Id), links.Unsubscribe(user.Id)), cancellationToken);
        }
    }

    private Task<List<User>> RecipientsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        db.Users
            .Where(u => userIds.Contains(u.Id) && u.EmailRemindersEnabled && u.Email != "")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    private async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await sender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogSendFailed(exception, message.Subject);
        }
    }

    [LoggerMessage(LogLevel.Error, "Failed to send email '{Subject}'")]
    private partial void LogSendFailed(Exception exception, string subject);
}
