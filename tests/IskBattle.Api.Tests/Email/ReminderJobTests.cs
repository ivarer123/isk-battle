using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;
using IskBattle.Domain.Entities;
using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Tests.Email;

/// <summary>
/// Runs <see cref="ReminderJob"/> against SQL Server. The job scans every user in the shared database,
/// so assertions only look at emails to the users each test creates.
/// </summary>
public class ReminderJobTests(SqlServerFixture sql)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly RecordingEmailSender _emails = new();

    // Saturday 26 September 2026, 08:30 in Stockholm (CEST): reminders for Friday the 25th are due.
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 9, 26, 6, 30, 0, TimeSpan.Zero));

    private ReminderJob Job()
    {
        var emailOptions = Options.Create(new EmailOptions { UnsubscribeKey = ApiFactory.TestUnsubscribeKey });
        var links = new EmailLinks(Options.Create(new AppLinkOptions { AppBaseUrl = "https://app.test", ApiBaseUrl = "https://api.test" }),
            new UnsubscribeTokens(emailOptions));
        return new ReminderJob(sql.CreateDbContext(), _emails, links, _clock, NullLogger<ReminderJob>.Instance);
    }

    private async Task<User> PlayerAsync(Cadence? leagueCadence, bool reminders = true, DateOnly? checkIn = null, MemberStatus status = MemberStatus.Active)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), ExternalId = Guid.NewGuid().ToString(), Email = $"{Guid.NewGuid():N}@example.com",
            DisplayName = "Anna", EmailRemindersEnabled = reminders, CreatedAt = DateTimeOffset.UtcNow,
        };
        await using var db = sql.CreateDbContext();
        db.Users.Add(user);
        if (leagueCadence is { } cadence)
        {
            var league = new League
            {
                Id = Guid.NewGuid(), Name = "Liga", Cadence = cadence, InviteCode = Guid.NewGuid().ToString("N")[..8],
                CreatedAt = DateTimeOffset.UtcNow,
            };
            league.Members.Add(new LeagueMember { LeagueId = league.Id, UserId = user.Id, Role = MemberRole.Owner, Status = status, JoinedAt = DateTimeOffset.UtcNow });
            db.Leagues.Add(league);
        }
        if (checkIn is { } date)
            db.CheckIns.Add(new CheckIn { Id = Guid.NewGuid(), UserId = user.Id, ForDate = date, YtdPercent = 1m, RecordedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(Ct);
        return user;
    }

    private int SentTo(User user) => _emails.Sent.Count(e => e.To == user.Email);

    [Fact]
    public async Task Weekly_player_without_check_in_for_friday_is_reminded_once()
    {
        var anna = await PlayerAsync(Cadence.Weekly, checkIn: new DateOnly(2026, 9, 18));

        await Job().RunAsync(Ct);
        await Job().RunAsync(Ct);

        Assert.Equal(1, SentTo(anna));
        var email = _emails.Sent.Single(e => e.To == anna.Email);
        Assert.Equal("Dags att uppdatera din utveckling", email.Subject);
        Assert.StartsWith("https://api.test/unsubscribe?token=", email.UnsubscribeUrl);
        await using var db = sql.CreateDbContext();
        Assert.Equal("W:2026-09-25", (await db.Users.SingleAsync(u => u.Id == anna.Id, Ct)).LastReminderWindowKey);
    }

    [Fact]
    public async Task Concurrent_runs_send_one_reminder()
    {
        var anna = await PlayerAsync(Cadence.Weekly);

        await Task.WhenAll(Job().RunAsync(Ct), Job().RunAsync(Ct), Job().RunAsync(Ct));

        Assert.Equal(1, SentTo(anna));
    }

    [Fact]
    public async Task Next_window_reminds_again()
    {
        var anna = await PlayerAsync(Cadence.Weekly);
        await Job().RunAsync(Ct);

        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 3, 6, 30, 0, TimeSpan.Zero));
        await Job().RunAsync(Ct);

        Assert.Equal(2, SentTo(anna));
    }

    [Fact]
    public async Task No_reminder_for_checked_in_opted_out_league_less_or_pending_players()
    {
        var checkedIn = await PlayerAsync(Cadence.Weekly, checkIn: new DateOnly(2026, 9, 25));
        var optedOut = await PlayerAsync(Cadence.Weekly, reminders: false);
        var noLeague = await PlayerAsync(null);
        var pending = await PlayerAsync(Cadence.Weekly, status: MemberStatus.Pending);
        var monthly = await PlayerAsync(Cadence.Monthly); // reminded on 1 Oct, not today

        await Job().RunAsync(Ct);

        Assert.All(new[] { checkedIn, optedOut, noLeague, pending, monthly }, u => Assert.Equal(0, SentTo(u)));
    }

    [Fact]
    public async Task Most_frequent_cadence_wins()
    {
        var anna = await PlayerAsync(Cadence.Monthly);
        await using (var db = sql.CreateDbContext())
        {
            var daily = new League
            {
                Id = Guid.NewGuid(), Name = "Daglig", Cadence = Cadence.Daily, InviteCode = Guid.NewGuid().ToString("N")[..8],
                CreatedAt = DateTimeOffset.UtcNow,
            };
            daily.Members.Add(new LeagueMember { LeagueId = daily.Id, UserId = anna.Id, Role = MemberRole.Member, Status = MemberStatus.Active, JoinedAt = DateTimeOffset.UtcNow });
            db.Leagues.Add(daily);
            await db.SaveChangesAsync(Ct);
        }

        await Job().RunAsync(Ct);

        Assert.Equal(1, SentTo(anna));
        await using var read = sql.CreateDbContext();
        Assert.Equal("D:2026-09-25", (await read.Users.SingleAsync(u => u.Id == anna.Id, Ct)).LastReminderWindowKey);
    }

    [Fact]
    public async Task Failed_send_is_not_retried_in_the_same_window()
    {
        var anna = await PlayerAsync(Cadence.Weekly);
        _emails.Fail = true;
        await Job().RunAsync(Ct);
        _emails.Fail = false;

        await Job().RunAsync(Ct);

        Assert.Equal(0, SentTo(anna));
    }
}
