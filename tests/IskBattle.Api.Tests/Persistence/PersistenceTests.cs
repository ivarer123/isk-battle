using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;
using IskBattle.Domain.Entities;
using IskBattle.Domain.Periods;
using IskBattle.Domain.Standings;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Tests.Persistence;

public class PersistenceTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static User NewUser(string name = "Anna") => new()
    {
        Id = Guid.NewGuid(),
        ExternalId = Guid.NewGuid().ToString(),
        Email = $"{Guid.NewGuid():N}@example.com",
        DisplayName = name,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static League NewLeague() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Kompisligan",
        Cadence = Cadence.Weekly,
        Tier = LeagueTier.Free,
        InviteCode = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static CheckIn NewCheckIn(Guid userId, DateOnly date, decimal ytd) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, ForDate = date, YtdPercent = ytd, RecordedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Model_has_no_pending_changes()
    {
        await using var db = sql.CreateDbContext();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task One_check_in_per_user_and_date()
    {
        await using var db = sql.CreateDbContext();
        var user = NewUser();
        db.Users.Add(user);
        db.CheckIns.Add(NewCheckIn(user.Id, new DateOnly(2026, 9, 21), 1m));
        await db.SaveChangesAsync(Ct);

        db.CheckIns.Add(NewCheckIn(user.Id, new DateOnly(2026, 9, 21), 2m));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Ytd_keeps_four_decimals()
    {
        var user = NewUser();
        await using (var db = sql.CreateDbContext())
        {
            db.Users.Add(user);
            db.CheckIns.Add(NewCheckIn(user.Id, new DateOnly(2026, 9, 21), -99.9912m));
            await db.SaveChangesAsync(Ct);
        }

        await using var read = sql.CreateDbContext();
        Assert.Equal(-99.9912m, (await read.CheckIns.SingleAsync(c => c.UserId == user.Id, Ct)).YtdPercent);
    }

    [Fact]
    public async Task Soft_deleted_leagues_are_hidden()
    {
        var league = NewLeague();
        league.DeletedAt = DateTimeOffset.UtcNow;
        await using (var db = sql.CreateDbContext())
        {
            db.Leagues.Add(league);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = sql.CreateDbContext();
        Assert.False(await read.Leagues.AnyAsync(l => l.Id == league.Id, Ct));
        Assert.True(await read.Leagues.IgnoreQueryFilters().AnyAsync(l => l.Id == league.Id, Ct));
    }

    [Fact]
    public async Task Deleting_a_user_keeps_purchases_and_removes_personal_data()
    {
        var user = NewUser();
        var league = NewLeague();
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(), LeagueId = league.Id, BuyerUserId = user.Id,
            FromTier = LeagueTier.Free, ToTier = LeagueTier.Premium50, AmountOre = 9_900,
            StripeCheckoutSessionId = $"cs_test_{Guid.NewGuid():N}", Status = PurchaseStatus.Paid,
            ImmediateDeliveryConsentAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow,
        };
        league.Members.Add(new LeagueMember
        {
            LeagueId = league.Id, UserId = user.Id, Role = MemberRole.Owner,
            Status = MemberStatus.Active, JoinedAt = DateTimeOffset.UtcNow,
        });
        await using (var db = sql.CreateDbContext())
        {
            db.AddRange(user, league, purchase, NewCheckIn(user.Id, new DateOnly(2026, 9, 21), 1m));
            await db.SaveChangesAsync(Ct);
            db.Users.Remove(user);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = sql.CreateDbContext();
        Assert.Null((await read.Purchases.SingleAsync(p => p.Id == purchase.Id, Ct)).BuyerUserId);
        Assert.False(await read.CheckIns.AnyAsync(c => c.UserId == user.Id, Ct));
        Assert.False(await read.LeagueMembers.AnyAsync(m => m.UserId == user.Id, Ct));
    }

    [Fact]
    public async Task Concurrent_league_updates_conflict()
    {
        var league = NewLeague();
        await using (var db = sql.CreateDbContext())
        {
            db.Leagues.Add(league);
            await db.SaveChangesAsync(Ct);
        }

        await using var first = sql.CreateDbContext();
        await using var second = sql.CreateDbContext();
        (await first.Leagues.SingleAsync(l => l.Id == league.Id, Ct)).Tier = LeagueTier.Premium50;
        (await second.Leagues.SingleAsync(l => l.Id == league.Id, Ct)).Tier = LeagueTier.Premium100;
        await first.SaveChangesAsync(Ct);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    [Theory]
    [InlineData(PeriodType.Day)]
    [InlineData(PeriodType.Week)]
    [InlineData(PeriodType.Month)]
    [InlineData(PeriodType.Year)]
    public async Task Standings_query_gives_same_result_as_full_history(PeriodType type)
    {
        var random = new Random(42);
        var users = Enumerable.Range(0, 8).Select(i => NewUser($"Spelare {i}")).ToList();
        var checkIns = new List<CheckIn>();
        foreach (var user in users)
        {
            for (var date = new DateOnly(2026, 1, 1); date < new DateOnly(2027, 1, 10); date = date.AddDays(1))
            {
                if (random.NextDouble() < 0.3)
                    checkIns.Add(NewCheckIn(user.Id, date, Math.Round((decimal)(random.NextDouble() * 60 - 20), 2)));
            }
        }
        await using (var db = sql.CreateDbContext())
        {
            db.Users.AddRange(users);
            db.CheckIns.AddRange(checkIns);
            await db.SaveChangesAsync(Ct);
        }

        var members = users.Select(u => new StandingsMember(u.Id, u.DisplayName)).ToList();
        var ids = users.Select(u => u.Id).ToList();
        await using var read = sql.CreateDbContext();
        foreach (var date in new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 14), new DateOnly(2026, 9, 21),
                                     new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 2) })
        {
            var period = PeriodCalculator.For(type, date);
            var loaded = await read.CheckIns.ForStandingsAsync(ids, period, Ct);

            Assert.Equal(
                StandingsCalculator.Calculate(period, members, checkIns),
                StandingsCalculator.Calculate(period, members, loaded));
        }
    }
}
