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
    public static readonly TimeOnly ReminderTime = new(18, 0);
    public const string TimeZoneId = "Europe/Stockholm";

    /// <summary>Trading days in v1 = Monday–Friday. Swedish exchange holidays are a later refinement.</summary>
    public static readonly FrozenSet<DayOfWeek> TradingDays = new[]
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
    }.ToFrozenSet();

    /// <summary>Period returns are rounded to this many decimals before ranking, so ties match what players see.</summary>
    public const int ReturnDecimals = 2;
}
