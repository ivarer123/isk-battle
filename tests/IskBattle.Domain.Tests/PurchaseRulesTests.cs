using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;
using IskBattle.Domain.Purchases;

namespace IskBattle.Domain.Tests;

public class PurchaseRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    private static League FullFreeLeague(int pending)
    {
        var league = new League { Id = Guid.NewGuid(), Tier = LeagueTier.Free };
        for (var i = 0; i < 10 + pending; i++)
        {
            league.Members.Add(new LeagueMember
            {
                LeagueId = league.Id, UserId = Guid.NewGuid(), JoinedAt = T0.AddMinutes(i),
                Role = i == 0 ? MemberRole.Owner : MemberRole.Member,
                Status = i < 10 ? MemberStatus.Active : MemberStatus.Pending,
            });
        }
        return league;
    }

    [Theory]
    [InlineData(LeagueTier.Premium50, true, CheckoutError.None)]
    [InlineData(LeagueTier.Premium50, false, CheckoutError.ConsentRequired)]
    [InlineData(LeagueTier.Free, true, CheckoutError.TierNotHigher)]
    [InlineData((LeagueTier)99, true, CheckoutError.TierNotHigher)]
    public void Checkout_validation(LeagueTier target, bool consent, CheckoutError expected) =>
        Assert.Equal(expected, PurchaseRules.ValidateCheckout(new League { Tier = LeagueTier.Free }, target, consent));

    [Fact]
    public void Already_at_tier_cannot_check_out_again() =>
        Assert.Equal(CheckoutError.TierNotHigher,
            PurchaseRules.ValidateCheckout(new League { Tier = LeagueTier.Premium100 }, LeagueTier.Premium50, true));

    [Fact]
    public void Purchase_costs_the_difference_and_records_consent()
    {
        var league = new League { Id = Guid.NewGuid(), Tier = LeagueTier.Premium50 };
        var buyer = Guid.NewGuid();

        var purchase = PurchaseRules.Create(league, buyer, LeagueTier.Premium100, T0);

        Assert.Equal((LeagueTier.Premium50, LeagueTier.Premium100, 10_000, PurchaseStatus.Created, T0, (Guid?)buyer),
            (purchase.FromTier, purchase.ToTier, purchase.AmountOre, purchase.Status, purchase.ImmediateDeliveryConsentAt, purchase.BuyerUserId));
    }

    [Fact]
    public void Paid_upgrade_promotes_buyer_and_activates_pending_in_order()
    {
        var league = FullFreeLeague(pending: 3);
        var buyer = league.Members[4];
        var purchase = PurchaseRules.Create(league, buyer.UserId, LeagueTier.Premium50, T0);

        var result = PurchaseRules.CompletePaid(league, purchase, T0.AddMinutes(5));

        Assert.Equal(PurchaseStatus.Paid, result.Status);
        Assert.Equal((LeagueTier.Premium50, PurchaseStatus.Paid, (DateTimeOffset?)T0.AddMinutes(5)), (league.Tier, purchase.Status, purchase.CompletedAt));
        Assert.Equal((MemberRole.CoHost, (DateTimeOffset?)T0.AddMinutes(5)), (buyer.Role, buyer.PromotedAt));
        Assert.Equal(league.Members.Skip(10).Select(m => m.UserId), result.Activated.Select(m => m.UserId));
        Assert.Equal(13, league.ActiveCount());
    }

    [Fact]
    public void Pending_buyer_is_activated_as_co_host_ahead_of_the_queue()
    {
        var league = FullFreeLeague(pending: 2);
        var buyer = league.Members[11]; // second in the queue
        var purchase = PurchaseRules.Create(league, buyer.UserId, LeagueTier.Premium50, T0);

        var result = PurchaseRules.CompletePaid(league, purchase, T0);

        Assert.Equal((MemberStatus.Active, MemberRole.CoHost), (buyer.Status, buyer.Role));
        Assert.Equal([buyer.UserId, league.Members[10].UserId], result.Activated.Select(m => m.UserId));
    }

    [Fact]
    public void Owner_paying_stays_owner()
    {
        var league = FullFreeLeague(pending: 0);
        var owner = league.Members[0];

        PurchaseRules.CompletePaid(league, PurchaseRules.Create(league, owner.UserId, LeagueTier.Premium100, T0), T0);

        Assert.Equal((MemberRole.Owner, LeagueTier.Premium100), (owner.Role, league.Tier));
    }

    [Fact]
    public void Buyer_who_left_or_deleted_their_account_still_upgrades_the_league()
    {
        var league = FullFreeLeague(pending: 1);
        var left = PurchaseRules.Create(league, Guid.NewGuid(), LeagueTier.Premium50, T0);
        Assert.Equal(PurchaseStatus.Paid, PurchaseRules.CompletePaid(league, left, T0).Status);
        var deleted = PurchaseRules.Create(league, Guid.NewGuid(), LeagueTier.Premium100, T0);
        deleted.BuyerUserId = null;

        Assert.Equal(PurchaseStatus.Paid, PurchaseRules.CompletePaid(league, deleted, T0).Status);
        Assert.Equal(LeagueTier.Premium100, league.Tier);
    }

    [Fact]
    public void Concurrent_buyer_at_same_tier_needs_refund()
    {
        var league = FullFreeLeague(pending: 0);
        var first = PurchaseRules.Create(league, league.Members[1].UserId, LeagueTier.Premium50, T0);
        var second = PurchaseRules.Create(league, league.Members[2].UserId, LeagueTier.Premium50, T0);
        PurchaseRules.CompletePaid(league, first, T0);

        var result = PurchaseRules.CompletePaid(league, second, T0);

        Assert.Equal(PurchaseStatus.NeedsRefund, result.Status);
        Assert.Equal(PurchaseStatus.NeedsRefund, second.Status);
        Assert.Equal(MemberRole.Member, league.Members[2].Role);
    }

    [Fact]
    public void League_upgraded_after_checkout_is_charged_too_much_and_needs_a_partial_refund()
    {
        // B starts Free -> 100 (199 kr); A completes Free -> 50 first, so B only owed 100 kr.
        var league = FullFreeLeague(pending: 0);
        var small = PurchaseRules.Create(league, league.Members[1].UserId, LeagueTier.Premium50, T0);
        var big = PurchaseRules.Create(league, league.Members[2].UserId, LeagueTier.Premium100, T0);
        PurchaseRules.CompletePaid(league, small, T0);

        var result = PurchaseRules.CompletePaid(league, big, T0);

        Assert.Equal(PurchaseStatus.PaidNeedsPartialRefund, result.Status);
        Assert.Equal((LeagueTier.Premium100, 9_900, 0), (league.Tier, big.RefundDueOre, small.RefundDueOre));
        Assert.Equal(MemberRole.CoHost, league.Members[2].Role);
    }

    [Theory]
    [InlineData(PurchaseStatus.NeedsRefund)]
    [InlineData(PurchaseStatus.PaidNeedsPartialRefund)]
    public void Completed_refund_cases_are_not_re_evaluated(PurchaseStatus status)
    {
        var league = FullFreeLeague(pending: 0);
        var purchase = PurchaseRules.Create(league, league.Members[1].UserId, LeagueTier.Premium50, T0);
        purchase.Status = status;

        var again = PurchaseRules.CompletePaid(league, purchase, T0);

        Assert.Equal((status, 0, LeagueTier.Free, (DateTimeOffset?)null), (again.Status, again.Activated.Count, league.Tier, purchase.CompletedAt));
    }

    [Fact]
    public void Deleted_league_needs_refund()
    {
        var league = FullFreeLeague(pending: 0);
        var purchase = PurchaseRules.Create(league, league.Members[1].UserId, LeagueTier.Premium50, T0);
        league.DeletedAt = T0;

        Assert.Equal(PurchaseStatus.NeedsRefund, PurchaseRules.CompletePaid(league, purchase, T0).Status);
        Assert.Equal((LeagueTier.Free, 9_900), (league.Tier, purchase.RefundDueOre));
    }

    [Fact]
    public void Completing_twice_is_a_no_op()
    {
        var league = FullFreeLeague(pending: 0);
        var purchase = PurchaseRules.Create(league, league.Members[1].UserId, LeagueTier.Premium50, T0);
        PurchaseRules.CompletePaid(league, purchase, T0);

        var again = PurchaseRules.CompletePaid(league, purchase, T0.AddHours(1));

        Assert.Equal((PurchaseStatus.Paid, 0, (DateTimeOffset?)T0), (again.Status, again.Activated.Count, purchase.CompletedAt));
    }

    [Fact]
    public void Failure_only_applies_to_open_purchases()
    {
        var open = new Purchase { Status = PurchaseStatus.Created };
        var paid = new Purchase { Status = PurchaseStatus.Paid };

        PurchaseRules.MarkFailed(open, T0);
        PurchaseRules.MarkFailed(paid, T0);

        Assert.Equal((PurchaseStatus.Failed, PurchaseStatus.Paid), (open.Status, paid.Status));
    }
}
