using IskBattle.Domain.Entities;

namespace IskBattle.Infrastructure.Notifications;

/// <summary>League emails (GAME-DESIGN.md §9). Implementations must not throw for delivery failures.</summary>
public interface INotificationService
{
    /// <summary>"{Namn} vill gå med i {Liga}. Uppgradera för att släppa in fler." to owner and co-hosts.</summary>
    Task JoinPendingAsync(League league, User joiner, CancellationToken cancellationToken = default);

    /// <summary>Tells members they were let in after an upgrade or a freed slot.</summary>
    Task MembersActivatedAsync(League league, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
