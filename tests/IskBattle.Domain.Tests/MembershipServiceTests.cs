using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;

namespace IskBattle.Domain.Tests;

public class MembershipServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private static League LeagueWith(LeagueTier tier, int activeMembers)
    {
        var league = new League { Id = Guid.NewGuid(), Tier = tier };
        for (var i = 0; i < activeMembers; i++)
        {
            league.Members.Add(new LeagueMember
            {
                LeagueId = league.Id, UserId = Guid.NewGuid(), Status = MemberStatus.Active,
                Role = i == 0 ? MemberRole.Owner : MemberRole.Member, JoinedAt = T0.AddMinutes(i),
            });
        }
        return league;
    }

    private static Guid Owner(League league) => league.Members.Single(m => m.Role == MemberRole.Owner).UserId;

    [Fact]
    public void Join_below_capacity_is_active()
    {
        var league = LeagueWith(LeagueTier.Free, 9);
        var user = Guid.NewGuid();

        Assert.Equal(JoinOutcome.Joined, MembershipService.Join(league, user, T0));
        Assert.Equal(MemberStatus.Active, league.Member(user)!.Status);
        Assert.Equal(MemberRole.Member, league.Member(user)!.Role);
    }

    [Fact]
    public void Join_full_league_is_pending()
    {
        var league = LeagueWith(LeagueTier.Free, 10);
        var user = Guid.NewGuid();

        Assert.Equal(JoinOutcome.Pending, MembershipService.Join(league, user, T0));
        Assert.Equal(MemberStatus.Pending, league.Member(user)!.Status);
    }

    [Fact]
    public void Joining_twice_changes_nothing()
    {
        var league = LeagueWith(LeagueTier.Free, 3);

        Assert.Equal(JoinOutcome.AlreadyMember, MembershipService.Join(league, Owner(league), T0));
        Assert.Equal(3, league.Members.Count);
    }

    [Fact]
    public void Upgrade_activates_pending_first_come_first_served_up_to_capacity()
    {
        var league = LeagueWith(LeagueTier.Free, 10);
        var pending = Enumerable.Range(0, 45).Select(_ => Guid.NewGuid()).ToList();
        for (var i = pending.Count - 1; i >= 0; i--) // insertion order must not matter
            MembershipService.Join(league, pending[i], T0.AddHours(1 + i));

        league.Tier = LeagueTier.Premium50;
        var activated = MembershipService.ActivatePending(league);

        Assert.Equal(pending.Take(40), activated.Select(m => m.UserId));
        Assert.Equal(50, league.ActiveCount());
        Assert.All(pending.Skip(40), id => Assert.Equal(MemberStatus.Pending, league.Member(id)!.Status));
    }

    [Fact]
    public void Leaving_frees_a_slot_for_the_first_pending_member()
    {
        var league = LeagueWith(LeagueTier.Free, 10);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        MembershipService.Join(league, second, T0.AddHours(2));
        MembershipService.Join(league, first, T0.AddHours(1));
        var leaver = league.Members[5].UserId;

        var activated = MembershipService.Remove(league, leaver, T0.AddDays(1));

        Assert.Equal(first, Assert.Single(activated).UserId);
        Assert.Equal(MemberStatus.Pending, league.Member(second)!.Status);
        Assert.Null(league.Member(leaver));
    }

    [Fact]
    public void Owner_leaving_passes_ownership_to_earliest_promoted_co_host()
    {
        var league = LeagueWith(LeagueTier.Free, 4);
        var early = league.Members[3];   // joined last, paid first
        var late = league.Members[1];
        MembershipService.PromoteToCoHost(league, late.UserId, T0.AddDays(2));
        MembershipService.PromoteToCoHost(league, early.UserId, T0.AddDays(1));

        MembershipService.Remove(league, Owner(league), T0.AddDays(3));

        Assert.Equal(MemberRole.Owner, early.Role);
        Assert.Equal(MemberRole.CoHost, late.Role);
    }

    [Fact]
    public void Owner_leaving_without_co_hosts_passes_to_longest_standing_active_member()
    {
        var league = LeagueWith(LeagueTier.Free, 10);
        var pendingEarlier = Guid.NewGuid();
        var owner = Owner(league);
        league.Members.Add(new LeagueMember
        {
            LeagueId = league.Id, UserId = pendingEarlier, Role = MemberRole.Member, Status = MemberStatus.Pending,
            JoinedAt = T0.AddDays(-1),
        });

        MembershipService.Remove(league, owner, T0.AddDays(1));

        Assert.Equal(MemberRole.Owner, league.Members.Single(m => m.JoinedAt == T0.AddMinutes(1)).Role);
        Assert.Equal(MemberRole.Member, league.Member(pendingEarlier)!.Role); // activated into the slot, not owner
        Assert.Equal(MemberStatus.Active, league.Member(pendingEarlier)!.Status);
    }

    [Fact]
    public void Last_member_leaving_soft_deletes_the_league()
    {
        var league = LeagueWith(LeagueTier.Free, 1);

        MembershipService.Remove(league, Owner(league), T0);

        Assert.Equal(T0, league.DeletedAt);
    }

    [Fact]
    public void Remaining_pending_member_takes_over_instead_of_deleting()
    {
        var league = LeagueWith(LeagueTier.Free, 1);
        var pending = Guid.NewGuid();
        league.Members.Add(new LeagueMember
        {
            LeagueId = league.Id, UserId = pending, Role = MemberRole.Member, Status = MemberStatus.Pending, JoinedAt = T0,
        });

        MembershipService.Remove(league, Owner(league), T0);

        Assert.Null(league.DeletedAt);
        Assert.Equal((MemberStatus.Active, MemberRole.Owner), (league.Member(pending)!.Status, league.Member(pending)!.Role));
    }

    [Fact]
    public void Removing_a_non_member_throws() =>
        Assert.Throws<InvalidOperationException>(() => MembershipService.Remove(LeagueWith(LeagueTier.Free, 1), Guid.NewGuid(), T0));

    [Fact]
    public void Pending_payer_becomes_active_co_host()
    {
        var league = LeagueWith(LeagueTier.Free, 10);
        var payer = Guid.NewGuid();
        MembershipService.Join(league, payer, T0);

        MembershipService.PromoteToCoHost(league, payer, T0.AddHours(1));

        Assert.Equal((MemberStatus.Active, MemberRole.CoHost, (DateTimeOffset?)T0.AddHours(1)),
            (league.Member(payer)!.Status, league.Member(payer)!.Role, league.Member(payer)!.PromotedAt));
    }

    [Fact]
    public void Owner_paying_stays_owner_and_co_host_keeps_first_promotion_time()
    {
        var league = LeagueWith(LeagueTier.Free, 2);
        var coHost = league.Members[1];
        MembershipService.PromoteToCoHost(league, Owner(league), T0);
        MembershipService.PromoteToCoHost(league, coHost.UserId, T0);
        MembershipService.PromoteToCoHost(league, coHost.UserId, T0.AddDays(5));

        Assert.Equal(MemberRole.Owner, league.Members[0].Role);
        Assert.Null(league.Members[0].PromotedAt);
        Assert.Equal(T0, coHost.PromotedAt);
    }

    [Fact]
    public void Promoting_a_non_member_throws() =>
        Assert.Throws<InvalidOperationException>(() => MembershipService.PromoteToCoHost(LeagueWith(LeagueTier.Free, 1), Guid.NewGuid(), T0));

    [Theory]
    [InlineData(MemberRole.Owner, MemberRole.Member, true)]
    [InlineData(MemberRole.CoHost, MemberRole.Member, true)]
    [InlineData(MemberRole.Owner, MemberRole.CoHost, false)]
    [InlineData(MemberRole.CoHost, MemberRole.Owner, false)]
    [InlineData(MemberRole.Member, MemberRole.Member, false)]
    public void Who_can_remove_whom(MemberRole actorRole, MemberRole targetRole, bool expected)
    {
        var actor = new LeagueMember { UserId = Guid.NewGuid(), Role = actorRole, Status = MemberStatus.Active };
        var target = new LeagueMember { UserId = Guid.NewGuid(), Role = targetRole, Status = MemberStatus.Active };

        Assert.Equal(expected, MembershipService.CanRemove(actor, target));
    }

    [Fact]
    public void Anyone_can_remove_themselves()
    {
        var self = new LeagueMember { UserId = Guid.NewGuid(), Role = MemberRole.Owner, Status = MemberStatus.Active };

        Assert.True(MembershipService.CanRemove(self, self));
    }

    [Fact]
    public void Pending_host_has_no_host_rights() =>
        Assert.False(new LeagueMember { Role = MemberRole.CoHost, Status = MemberStatus.Pending }.IsHost());
}
