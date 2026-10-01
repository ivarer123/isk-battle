namespace IskBattle.Domain.Entities;

public class LeagueMember
{
    public Guid LeagueId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; }
    public MemberStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }   // FIFO order for pending activation
}
