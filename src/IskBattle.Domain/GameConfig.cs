using System.Collections.Frozen;

namespace IskBattle.Domain;

/// <summary>All tunables live here. Nothing in the domain hard-codes these values.</summary>
public static class GameConfig
{
    public static readonly IReadOnlyDictionary<LeagueTier, (int Capacity, int PriceOre)> Tiers =
        new Dictionary<LeagueTier, (int, int)>
        {
            [LeagueTier.Free]       = (10, 0),
            [LeagueTier.Premium50]  = (50, 9_900),
            [LeagueTier.Premium100] = (100, 19_900),
            [LeagueTier.Premium250] = (250, 39_900),
        }.ToFrozenDictionary();

    public static int UpgradePriceOre(LeagueTier from, LeagueTier to) =>
        Tiers[to].PriceOre - Tiers[from].PriceOre;

    public const decimal MinYtdPercent = -99.99m;
    public const decimal MaxYtdPercent = 1000m;
    public const decimal ConfirmJumpThresholdPp = 20m;
    /// <summary>Check-in windows open after the US close (22:00, 21:00 in the DST gap weeks) with margin.</summary>
    public static readonly TimeOnly CheckInWindowOpens = new(22, 30);

    /// <summary>Daily and Monthly windows close before Stockholm opens, so no reading includes the next day.</summary>
    public static readonly TimeOnly CheckInWindowCloses = new(9, 0);

    /// <summary>Reminders go out the morning after the trading day, inside the open window.</summary>
    public static readonly TimeOnly ReminderTime = new(8, 0);

    /// <summary>The season's final window closes at midnight, so its reminder goes out the same evening.</summary>
    public static readonly TimeOnly SeasonEndReminderTime = new(20, 0);

    /// <summary>
    /// How long after a reminder time a scheduled reminder run may still do work. The job's cron fires at two UTC
    /// hours to cover daylight saving; the run outside this span exits without touching the database.
    /// </summary>
    public static readonly TimeSpan ReminderRunSpan = TimeSpan.FromHours(1);
    public const string TimeZoneId = "Europe/Stockholm";

    /// <summary>Trading days in v1 = Monday–Friday. Swedish exchange holidays are a later refinement.</summary>
    public static readonly FrozenSet<DayOfWeek> TradingDays = new[]
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    }.ToFrozenSet();

    /// <summary>Seasons the API accepts in queries; anything outside is a client error, not a calendar edge case.</summary>
    public const int FirstSupportedSeason = 2000;
    public const int LastSupportedSeason = 2999;

    /// <summary>Period returns are rounded to this many decimals before ranking, so ties match what players see.</summary>
    public const int ReturnDecimals = 2;
}
