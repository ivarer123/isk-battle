using IskBattle.Domain.Entities;
using IskBattle.Domain.Time;

namespace IskBattle.Domain.CheckIns;

public enum CheckInOutcome { Accepted, OutOfRange, RequiresConfirmation }

/// <param name="ForDate">The Stockholm date the check-in is stored against (always today).</param>
/// <param name="IsOverwrite">True when a check-in for today already exists and will be replaced.</param>
/// <param name="PreviousYtdPercent">The check-in compared against for the jump guard, if any.</param>
public sealed record CheckInValidation(
    CheckInOutcome Outcome,
    DateOnly ForDate,
    bool IsOverwrite,
    decimal? PreviousYtdPercent);

/// <summary>
/// Validates a new check-in (GAME-DESIGN.md §4). A check-in always targets today's Stockholm date, so
/// only today's check-in can ever be overwritten and past check-ins stay immutable.
/// <para>
/// The jump guard compares against the most recent check-in dated before today in the same season.
/// The first check-in of a season (YTD reset) never asks for confirmation.
/// </para>
/// </summary>
public static class CheckInValidator
{
    /// <param name="history">The player's own check-ins.</param>
    public static CheckInValidation Validate(
        decimal ytdPercent,
        IEnumerable<CheckIn> history,
        TimeProvider time,
        bool confirmed)
    {
        var today = StockholmTime.Today(time);
        var checkIns = history.ToList();
        var isOverwrite = checkIns.Exists(c => c.ForDate == today);
        var previous = checkIns
            .Where(c => c.Season == today.Year && c.ForDate < today)
            .MaxBy(c => c.ForDate)
            ?.YtdPercent;

        if (ytdPercent is < GameConfig.MinYtdPercent or > GameConfig.MaxYtdPercent)
            return new CheckInValidation(CheckInOutcome.OutOfRange, today, isOverwrite, previous);

        if (!confirmed && previous is { } p && Math.Abs(ytdPercent - p) > GameConfig.ConfirmJumpThresholdPp)
            return new CheckInValidation(CheckInOutcome.RequiresConfirmation, today, isOverwrite, previous);

        return new CheckInValidation(CheckInOutcome.Accepted, today, isOverwrite, previous);
    }
}
