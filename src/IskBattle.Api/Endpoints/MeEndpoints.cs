using IskBattle.Api.Auth;
using IskBattle.Domain;
using IskBattle.Domain.Leagues;
using IskBattle.Domain.Time;
using IskBattle.Infrastructure.Notifications;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Endpoints;

internal static class MeEndpoints
{
    public const int MaxDisplayNameLength = 50;

    public static void MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", GetMe);
        api.MapPatch("/me", UpdateMe);
        api.MapDelete("/me", DeleteMe);
    }

    private static async Task<MeResponse> GetMe(HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var user = http.GetCurrentUser();
        var today = StockholmTime.Today(time);
        var cadences = await db.Leagues
            .Where(l => l.Members.Any(m => m.UserId == user.Id && m.Status == MemberStatus.Active))
            .ToListAsync(http.RequestAborted);
        var last = await db.CheckIns
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.ForDate)
            .Select(c => new CheckInResponse(c.ForDate, c.YtdPercent))
            .FirstOrDefaultAsync(http.RequestAborted);

        return new MeResponse(
            user.Id,
            user.DisplayName,
            user.Email,
            user.EmailRemindersEnabled,
            LeagueRules.MostFrequent(cadences.Select(l => l.CadenceFor(today.Year))),
            today,
            last);
    }

    private static async Task<IResult> UpdateMe(UpdateMeRequest request, HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var user = http.GetCurrentUser();
        if (request.DisplayName is not null)
        {
            var name = request.DisplayName.Trim();
            if (name.Length is 0 or > MaxDisplayNameLength)
                return Problems.BadRequest("display_name_invalid", $"Display name must be 1–{MaxDisplayNameLength} characters.");
            user.DisplayName = name;
        }
        if (request.EmailRemindersEnabled is { } reminders)
            user.EmailRemindersEnabled = reminders;

        await db.SaveChangesAsync(http.RequestAborted);
        return Results.Ok(await GetMe(http, db, time));
    }

    /// <summary>
    /// GDPR delete: leaves every league (with ownership transfer), then removes the user. Check-ins and
    /// memberships cascade; purchases keep their bookkeeping record with the buyer reference nulled.
    /// </summary>
    private static async Task<IResult> DeleteMe(
        HttpContext http, IskBattleDbContext db, TimeProvider time, INotificationService notifications)
    {
        var user = http.GetCurrentUser();
        var now = time.GetUtcNow();
        var leagues = await db.Leagues.Include(l => l.Members)
            .Where(l => l.Members.Any(m => m.UserId == user.Id))
            .ToListAsync(http.RequestAborted);

        var activations = leagues
            .Select(league => (league, activated: MembershipService.Remove(league, user.Id, now)))
            .ToList();
        db.Users.Remove(user);
        await db.SaveChangesAsync(http.RequestAborted);

        foreach (var (league, activated) in activations.Where(a => a.activated.Count > 0))
            await notifications.MembersActivatedAsync(league, [.. activated.Select(m => m.UserId)], http.RequestAborted);

        return Results.NoContent();
    }
}
