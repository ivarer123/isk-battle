using IskBattle.Domain.Entities;
using IskBattle.Domain.Periods;

namespace IskBattle.Domain.Leagues;

/// <summary>League-level rules: capacity, cadence per season, visible periods and host rights (GAME-DESIGN.md §5, §7, §8).</summary>
public static class LeagueRules
{
    public static int Capacity(this League league) => GameConfig.Tiers[league.Tier].Capacity;

    public static int ActiveCount(this League league) => league.Members.Count(m => m.Status == MemberStatus.Active);

    public static LeagueMember? Member(this League league, Guid userId) =>
        league.Members.Find(m => m.UserId == userId);

    public static bool IsHost(this LeagueMember member) =>
        member is { Status: MemberStatus.Active, Role: MemberRole.Owner or MemberRole.CoHost };

    /// <summary>
    /// Cadence changes only take effect between seasons. There is no rollover job: a queued change applies
    /// from <see cref="League.NextCadenceSeason"/> onwards.
    /// </summary>
    public static Cadence CadenceFor(this League league, int season) =>
        league.NextSeasonCadence is { } next && season >= league.NextCadenceSeason ? next : league.Cadence;

    /// <summary>Queues a cadence for the season after <paramref name="currentSeason"/>, or clears the queue with null.</summary>
    public static void QueueCadence(this League league, Cadence? cadence, int currentSeason)
    {
        league.Cadence = league.CadenceFor(currentSeason);
        league.NextSeasonCadence = cadence == league.Cadence ? null : cadence;
        league.NextCadenceSeason = league.NextSeasonCadence is null ? null : currentSeason + 1;
    }

    /// <summary>Standings tabs shown for a cadence: the cadence itself and everything coarser.</summary>
    public static IReadOnlyList<PeriodType> VisiblePeriods(Cadence cadence) => cadence switch
    {
        Cadence.Daily => [PeriodType.Day, PeriodType.Week, PeriodType.Month, PeriodType.Year],
        Cadence.Weekly => [PeriodType.Week, PeriodType.Month, PeriodType.Year],
        Cadence.Monthly => [PeriodType.Month, PeriodType.Year],
        _ => throw new ArgumentOutOfRangeException(nameof(cadence), cadence, null),
    };

    /// <summary>The most frequent cadence among a player's active leagues, which drives their reminders. Null if none.</summary>
    public static Cadence? MostFrequent(IEnumerable<Cadence> cadences) =>
        cadences.Select(c => (Cadence?)c).Min();

    public static IEnumerable<(LeagueTier Tier, int Capacity, int PriceOre)> UpgradeOptions(this League league) =>
        GameConfig.Tiers
            .Where(t => t.Key > league.Tier)
            .OrderBy(t => t.Key)
            .Select(t => (t.Key, t.Value.Capacity, GameConfig.UpgradePriceOre(league.Tier, t.Key)));
}
