using IskBattle.Domain.Entities;
using IskBattle.Domain.Periods;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Infrastructure.Persistence;

public static class CheckInQueries
{
    /// <summary>
    /// Loads exactly the check-ins <see cref="Domain.Standings.StandingsCalculator"/> needs: per member, the latest
    /// check-in before the period start (same season) and the latest within the period. Both use the
    /// unique <c>(UserId, ForDate)</c> index.
    /// </summary>
    public static async Task<List<CheckIn>> ForStandingsAsync(
        this IQueryable<CheckIn> checkIns,
        IReadOnlyCollection<Guid> userIds,
        Period period,
        CancellationToken cancellationToken = default)
    {
        var bases = await checkIns
            .Where(c => userIds.Contains(c.UserId) && c.ForDate >= period.SeasonStart && c.ForDate < period.Start)
            .GroupBy(c => c.UserId)
            .Select(g => g.OrderByDescending(c => c.ForDate).First())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var latest = await checkIns
            .Where(c => userIds.Contains(c.UserId) && c.ForDate >= period.Start && c.ForDate < period.End)
            .GroupBy(c => c.UserId)
            .Select(g => g.OrderByDescending(c => c.ForDate).First())
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. bases, .. latest];
    }
}
