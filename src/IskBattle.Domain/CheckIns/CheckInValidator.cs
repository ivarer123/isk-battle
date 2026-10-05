using IskBattle.Domain.Entities;
using IskBattle.Domain.Time;

namespace IskBattle.Domain.CheckIns;

public enum CheckInOutcome { Accepted, WindowClosed, OutOfRange, RequiresConfirmation }

/// <param name="ForDate">The trading day the check-in reports on (the open window's day), or default when closed.</param>
/// <param name="IsOverwrite">True when a check-in for that trading day already exists and will be replaced.</param>
/// <param name="PreviousYtdPercent">The check-in compared against for the jump guard, if any.</param>
/// <param name="NextWindow">When closed: the next window, so the UI can say when it opens.</param>
public sealed record CheckInValidation(
    CheckInOutcome Outcome,
    DateOnly ForDate,
    bool IsOverwrite,
    decimal? PreviousYtdPercent,
    CheckInWindow? NextWindow = null);

/// <summary>
/// Validates a new check-in (GAME-DESIGN.md §4, §4a). A check-in is only accepted while a window is open and
/// is stored against the trading day it reports on, so it can be overwritten only within that window and is
/// immutable once the window closes.
/// <para>
/// The jump guard compares against the most recent check-in for an earlier trading day in the same season.
/// The first check-in of a season (YTD reset) never asks for confirmation.
/// </para>
/// </summary>
public static class CheckInValidator
{
    /// <param name="history">The player's own check-ins.</param>
    /// <param name="cadencesForSeason">The cadences of the player's active leagues per season (see <see cref="CheckInWindows.Open"/>).</param>
    public static CheckInValidation Validate(
        decimal ytdPercent,
        IEnumerable<CheckIn> history,
        TimeProvider time,
        Func<int, IReadOnlyCollection<Cadence>> cadencesForSeason,
        bool confirmed)
    {
        var now = StockholmTime.Now(time).DateTime;
        if (CheckInWindows.Open(now, cadencesForSeason) is not { } window)
            return new CheckInValidation(CheckInOutcome.WindowClosed, default, false, null, CheckInWindows.Next(now, cadencesForSeason));

        var day = window.TradingDay;
        var checkIns = history.ToList();
        var isOverwrite = checkIns.Exists(c => c.ForDate == day);
        var previous = checkIns
            .Where(c => c.Season == day.Year && c.ForDate < day)
            .MaxBy(c => c.ForDate)
            ?.YtdPercent;

        if (ytdPercent is < GameConfig.MinYtdPercent or > GameConfig.MaxYtdPercent)
            return new CheckInValidation(CheckInOutcome.OutOfRange, day, isOverwrite, previous);

        if (!confirmed && previous is { } p && Math.Abs(ytdPercent - p) > GameConfig.ConfirmJumpThresholdPp)
            return new CheckInValidation(CheckInOutcome.RequiresConfirmation, day, isOverwrite, previous);

        return new CheckInValidation(CheckInOutcome.Accepted, day, isOverwrite, previous);
    }
}
