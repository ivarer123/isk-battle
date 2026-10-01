using IskBattle.Domain;
using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;
using IskBattle.Infrastructure.Persistence;
using IskBattle.Api.Auth;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Leagues;

internal enum LeagueAccessLevel { AnyMember, ActiveMember, Host, Owner }

internal sealed record LeagueContext(League League, LeagueMember Me);

/// <summary>
/// Authorization for <c>/leagues/{id}</c> routes, enforced server-side (ARCHITECTURE.md §11).
/// Non-members get 404 so league ids reveal nothing.
/// </summary>
internal static class LeagueAccess
{
    private const string ItemKey = "IskBattle.League";

    public static LeagueContext GetLeague(this HttpContext http) => (LeagueContext)http.Items[ItemKey]!;

    public static RouteHandlerBuilder RequireLeague(this RouteHandlerBuilder builder, LeagueAccessLevel level) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!Guid.TryParse(http.GetRouteValue("id")?.ToString(), out var leagueId))
                return Problems.NotFound("league_not_found", "League not found.");

            var db = http.RequestServices.GetRequiredService<IskBattleDbContext>();
            var league = await db.Leagues.Include(l => l.Members)
                .SingleOrDefaultAsync(l => l.Id == leagueId, http.RequestAborted);
            var me = league?.Member(http.GetCurrentUser().Id);
            if (league is null || me is null)
                return Problems.NotFound("league_not_found", "League not found.");

            var allowed = level switch
            {
                LeagueAccessLevel.AnyMember => true,
                LeagueAccessLevel.ActiveMember => me.Status == MemberStatus.Active,
                LeagueAccessLevel.Host => me.IsHost(),
                _ => me is { Role: MemberRole.Owner, Status: MemberStatus.Active },
            };
            if (!allowed)
                return Problems.Forbidden("league_forbidden", "You don't have access to this.");

            http.Items[ItemKey] = new LeagueContext(league, me);
            return await next(context);
        });

    /// <summary>
    /// Saves membership changes. The league row is always updated so its rowversion guards capacity
    /// against concurrent joins.
    /// </summary>
    public static async Task<bool> TrySaveLeagueAsync(this IskBattleDbContext db, League league, CancellationToken ct)
    {
        if (db.Entry(league).State == EntityState.Unchanged)
            db.Entry(league).Property(l => l.Name).IsModified = true;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
