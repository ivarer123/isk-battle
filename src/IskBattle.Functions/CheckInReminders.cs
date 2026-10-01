using IskBattle.Infrastructure.Reminders;
using Microsoft.Azure.Functions.Worker;

namespace IskBattle.Functions;

/// <summary>Every 15 minutes; <see cref="ReminderJob"/> decides who is due and sends at most once per window.</summary>
public sealed class CheckInReminders(ReminderJob job)
{
    [Function(nameof(CheckInReminders))]
    public Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo timer, CancellationToken cancellationToken) =>
        job.RunAsync(cancellationToken);
}
