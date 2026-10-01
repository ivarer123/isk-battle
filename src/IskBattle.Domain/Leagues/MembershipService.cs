using IskBattle.Domain.Entities;

namespace IskBattle.Domain.Leagues;

public enum JoinOutcome { Joined, Pending, AlreadyMember }

/// <summary>
/// Membership rules (GAME-DESIGN.md §7). Works on a loaded <see cref="League"/> with its members.
/// <para>
/// Whenever a slot frees up or capacity grows, Pending members are activated first-come, first-served.
/// Ownership passes to the earliest co-host (by <see cref="LeagueMember.PromotedAt"/>), otherwise to the
/// longest-standing active member. A league with no active members left is soft-deleted.
/// </para>
/// </summary>
public static class MembershipService
{
    public static JoinOutcome Join(League league, Guid userId, DateTimeOffset now)
    {
        if (league.Member(userId) is not null)
            return JoinOutcome.AlreadyMember;

        var status = league.ActiveCount() < league.Capacity() ? MemberStatus.Active : MemberStatus.Pending;
        league.Members.Add(new LeagueMember
        {
            LeagueId = league.Id,
            UserId = userId,
            Role = MemberRole.Member,
            Status = status,
            JoinedAt = now,
        });
        return status == MemberStatus.Active ? JoinOutcome.Joined : JoinOutcome.Pending;
    }

    /// <summary>Removes a member (leaving or being removed) and returns members activated into the freed slot.</summary>
    public static IReadOnlyList<LeagueMember> Remove(League league, Guid userId, DateTimeOffset now)
    {
        var member = league.Member(userId) ?? throw new InvalidOperationException("Not a member of the league.");
        league.Members.Remove(member);

        // Ownership goes to someone who was already active; a Pending member's JoinedAt is when they queued.
        // Only if nobody active is left does an activated Pending member take over.
        if (member.Role == MemberRole.Owner)
            TransferOwnership(league);

        var activated = ActivatePending(league);

        if (!league.Members.Exists(m => m.Role == MemberRole.Owner))
            TransferOwnership(league);

        if (league.ActiveCount() == 0)
            league.DeletedAt = now;

        return activated;
    }

    /// <summary>Activates Pending members in join order up to capacity. Returns the activated members.</summary>
    public static IReadOnlyList<LeagueMember> ActivatePending(League league)
    {
        var free = league.Capacity() - league.ActiveCount();
        var activated = league.Members
            .Where(m => m.Status == MemberStatus.Pending)
            .OrderBy(m => m.JoinedAt)
            .Take(Math.Max(free, 0))
            .ToList();
        activated.ForEach(m => m.Status = MemberStatus.Active);
        return activated;
    }

    /// <summary>
    /// Makes a paying member co-host (GAME-DESIGN.md §8). A Pending payer is activated first; the owner keeps their role.
    /// </summary>
    public static void PromoteToCoHost(League league, Guid userId, DateTimeOffset now)
    {
        var member = league.Member(userId) ?? throw new InvalidOperationException("Not a member of the league.");
        member.Status = MemberStatus.Active;
        if (member.Role == MemberRole.Member)
        {
            member.Role = MemberRole.CoHost;
            member.PromotedAt = now;
        }
    }

    /// <summary>
    /// Hosts can remove ordinary members (Active or Pending). Co-hosts paid for the league and the owner
    /// can only leave themselves.
    /// </summary>
    public static bool CanRemove(LeagueMember actor, LeagueMember target) =>
        actor.UserId == target.UserId || (actor.IsHost() && target.Role == MemberRole.Member);

    private static void TransferOwnership(League league)
    {
        var active = league.Members.Where(m => m.Status == MemberStatus.Active).ToList();
        var next = active
                       .Where(m => m.Role == MemberRole.CoHost)
                       .OrderBy(m => m.PromotedAt)
                       .ThenBy(m => m.JoinedAt)
                       .FirstOrDefault()
                   ?? active.OrderBy(m => m.JoinedAt).FirstOrDefault();
        if (next is not null)
            next.Role = MemberRole.Owner;
    }
}
