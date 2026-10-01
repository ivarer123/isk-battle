using IskBattle.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IskBattle.Infrastructure.Notifications;

/// <summary>Placeholder until email delivery is wired up.</summary>
internal sealed partial class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    public Task JoinPendingAsync(League league, User joiner, CancellationToken cancellationToken = default)
    {
        LogJoinPending(league.Id, joiner.Id);
        return Task.CompletedTask;
    }

    public Task MembersActivatedAsync(League league, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
    {
        LogActivated(league.Id, userIds.Count);
        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Join pending in league {LeagueId} by user {UserId}")]
    private partial void LogJoinPending(Guid leagueId, Guid userId);

    [LoggerMessage(LogLevel.Information, "{Count} pending member(s) activated in league {LeagueId}")]
    private partial void LogActivated(Guid leagueId, int count);
}
