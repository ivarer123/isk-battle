using IskBattle.Domain;
using IskBattle.Domain.Leagues;
using IskBattle.Domain.Reminders;
using IskBattle.Domain.Time;
using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IskBattle.Infrastructure.Reminders;

/// <summary>
/// Sends check-in reminders (ARCHITECTURE.md §8). Runs on a cron schedule; outside the reminder run times it
/// returns before touching the database, so SQL serverless can stay auto-paused. Safe to run concurrently and
/// repeatedly: each user's window is claimed with a conditional update before sending, so at most one reminder
/// goes out per window.
/// </summary>
public sealed partial class ReminderJob(
    IskBattleDbContext db,
    IEmailSender sender,
    EmailLinks links,
    TimeProvider time,
    ILogger<ReminderJob> logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = StockholmTime.Now(time);
        if (!ReminderPolicy.IsRunTime(now))
        {
            LogNotRunTime(now);
            return 0;
        }
        var season = now.Year;

        var candidates = await db.Users
            .Where(u => u.EmailRemindersEnabled && u.Email != "")
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.LastReminderWindowKey,
                Leagues = db.Leagues
                    .Where(l => l.Members.Any(m => m.UserId == u.Id && m.Status == MemberStatus.Active))
                    .Select(l => new { l.Cadence, l.NextSeasonCadence, l.NextCadenceSeason })
                    .ToList(),
                LatestCheckIn = db.CheckIns.Where(c => c.UserId == u.Id).Max(c => (DateOnly?)c.ForDate),
            })
            .Where(u => u.Leagues.Any())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var user in candidates)
        {
            var cadence = LeagueRules.MostFrequent(user.Leagues.Select(l =>
                new Domain.Entities.League { Cadence = l.Cadence, NextSeasonCadence = l.NextSeasonCadence, NextCadenceSeason = l.NextCadenceSeason }
                    .CadenceFor(season)))!.Value;
            var decision = ReminderPolicy.Evaluate(cadence, now, user.LatestCheckIn);
            if (!decision.IsDue || decision.WindowKey == user.LastReminderWindowKey)
                continue;

            // Claim the window atomically; another run that got here first wins.
            var claimed = await db.Users
                .Where(u => u.Id == user.Id && (u.LastReminderWindowKey == null || u.LastReminderWindowKey != decision.WindowKey))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastReminderWindowKey, decision.WindowKey), cancellationToken);
            if (claimed == 0)
                continue;

            try
            {
                await sender.SendAsync(
                    EmailTemplates.CheckInReminder(user.Email, user.DisplayName, links.App(), links.Unsubscribe(user.Id)),
                    cancellationToken);
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogSendFailed(exception, user.Id);
            }
        }

        LogRun(sent, candidates.Count);
        return sent;
    }

    [LoggerMessage(LogLevel.Error, "Failed to send reminder to user {UserId}")]
    private partial void LogSendFailed(Exception exception, Guid userId);

    [LoggerMessage(LogLevel.Information, "Not a reminder run time ({StockholmNow}); exiting without database access")]
    private partial void LogNotRunTime(DateTimeOffset stockholmNow);

    [LoggerMessage(LogLevel.Information, "Reminders sent: {Sent} of {Candidates} candidates")]
    private partial void LogRun(int sent, int candidates);
}
