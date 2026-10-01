using IskBattle.Domain.Entities;
using IskBattle.Domain.Periods;

namespace IskBattle.Domain.Standings;

/// <summary>
/// Derives standings for one period from check-ins. Implements GAME-DESIGN.md §6.
/// <list type="bullet">
/// <item><b>latest</b>: the member's most recent check-in in <c>[start, end)</c>.</item>
/// <item><b>base</b>: the member's most recent check-in before <c>start</c> in the same season.
/// If there is none, <c>base = 0</c> when the period starts the season. Otherwise the member is "Ny",
/// and this takes precedence over "Pausad".</item>
/// <item>No latest: "Pausad", showing the last known YTD of the season.</item>
/// </list>
/// År needs no special case. Its period starts on 1 January, so the base is always 0 and latest is the
/// most recent check-in of the season.
/// </summary>
public static class StandingsCalculator
{
    public static IReadOnlyList<StandingsRow> Calculate(
        Period period,
        IEnumerable<StandingsMember> members,
        IEnumerable<CheckIn> checkIns)
    {
        var history = checkIns
            .Where(c => c.Season == period.Season && c.ForDate < period.End)
            .ToLookup(c => c.UserId);

        var rows = members
            .Select(m => Evaluate(period, m, history[m.UserId]))
            .OrderBy(r => r.Status)
            .ThenByDescending(r => r.PeriodReturn)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.UserId)
            .ToList();

        for (var i = 0; i < rows.Count && rows[i].Status == StandingStatus.Ranked; i++)
        {
            var rank = i > 0 && rows[i].PeriodReturn == rows[i - 1].PeriodReturn ? rows[i - 1].Rank : i + 1;
            rows[i] = rows[i] with { Rank = rank };
        }

        return rows;
    }

    /// <summary><c>((1 + latest/100) / (1 + base/100) - 1) * 100</c>, rounded to <see cref="GameConfig.ReturnDecimals"/>.</summary>
    public static decimal PeriodReturn(decimal latestYtd, decimal baseYtd) =>
        Math.Round(
            ((1m + latestYtd / 100m) / (1m + baseYtd / 100m) - 1m) * 100m,
            GameConfig.ReturnDecimals,
            MidpointRounding.AwayFromZero);

    private static StandingsRow Evaluate(Period period, StandingsMember member, IEnumerable<CheckIn> history)
    {
        var ordered = history.OrderBy(c => c.ForDate).ThenBy(c => c.RecordedAt).ToList();
        var latest = ordered.LastOrDefault(c => c.ForDate >= period.Start);
        var @base = ordered.LastOrDefault(c => c.ForDate < period.Start);
        var ytd = (latest ?? @base)?.YtdPercent;

        if (@base is null && !period.StartsSeason)
            return new StandingsRow(member.UserId, member.DisplayName, StandingStatus.Ny, null, null, ytd);

        if (latest is null)
            return new StandingsRow(member.UserId, member.DisplayName, StandingStatus.Pausad, null, null, ytd);

        var periodReturn = PeriodReturn(latest.YtdPercent, @base?.YtdPercent ?? 0m);
        return new StandingsRow(member.UserId, member.DisplayName, StandingStatus.Ranked, null, periodReturn, ytd);
    }
}
