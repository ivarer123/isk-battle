namespace IskBattle.Domain.Entities;

public class LeagueMember
{
    public Guid LeagueId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; }
    public MemberStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }   // FIFO order for pending activation
    public DateTimeOffset? PromotedAt { get; set; } // when the member became co-host; earliest inherits ownership
}
