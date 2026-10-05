namespace IskBattle.Domain.Entities;

/// <summary>
/// Hosts are emailed at most once per player and league about a pending join, so leaving and rejoining a
/// full league can't be used to send repeated email (GAME-DESIGN.md §7).
/// </summary>
public class JoinPendingNotice
{
    public Guid LeagueId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset SentAt { get; set; }
}
