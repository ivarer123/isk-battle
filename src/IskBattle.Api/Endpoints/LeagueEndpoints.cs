using System.Globalization;
using IskBattle.Api.Auth;
using IskBattle.Api.Leagues;
using IskBattle.Domain;
using IskBattle.Domain.Entities;
using IskBattle.Domain.Leagues;
using IskBattle.Domain.Periods;
using IskBattle.Domain.Standings;
using IskBattle.Domain.Time;
using IskBattle.Infrastructure.Notifications;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Endpoints;

internal static class LeagueEndpoints
{
    public const string JoinRateLimitPolicy = "join";
    public const int MaxNameLength = 60;

    public static void MapLeagueEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/leagues", Create);
        api.MapGet("/leagues", List);
        api.MapPost("/leagues/join", Join).RequireRateLimiting(JoinRateLimitPolicy);
        api.MapGet("/leagues/{id:guid}", Get).RequireLeague(LeagueAccessLevel.AnyMember);
        api.MapPatch("/leagues/{id:guid}", Update).RequireLeague(LeagueAccessLevel.Host);
        api.MapDelete("/leagues/{id:guid}", Delete).RequireLeague(LeagueAccessLevel.Owner);
        api.MapPost("/leagues/{id:guid}/invite-code", RegenerateInviteCode).RequireLeague(LeagueAccessLevel.Host);
        api.MapDelete("/leagues/{id:guid}/members/{userId:guid}", RemoveMember).RequireLeague(LeagueAccessLevel.AnyMember);
        api.MapGet("/leagues/{id:guid}/standings", Standings).RequireLeague(LeagueAccessLevel.ActiveMember);
    }

    private static async Task<IResult> Create(CreateLeagueRequest request, HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var name = request.Name?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength)
            return Problems.BadRequest("league_name_invalid", $"League name must be 1–{MaxNameLength} characters.");
        if (!Enum.IsDefined(request.Cadence))
            return Problems.BadRequest("cadence_invalid", "Unknown cadence.");

        var user = http.GetCurrentUser();
        var now = time.GetUtcNow();
        var league = new League
        {
            Id = Guid.NewGuid(),
            Name = name,
            Cadence = request.Cadence,
            Tier = LeagueTier.Free,
            InviteCode = await UnusedInviteCodeAsync(db, http.RequestAborted),
            CreatedAt = now,
        };
        league.Members.Add(new LeagueMember
        {
            LeagueId = league.Id, UserId = user.Id, Role = MemberRole.Owner, Status = MemberStatus.Active, JoinedAt = now,
        });
        db.Leagues.Add(league);
        await db.SaveChangesAsync(http.RequestAborted);

        var me = league.Members[0];
        return Results.Created($"/leagues/{league.Id}", await DetailAsync(db, league, me, time, http.RequestAborted));
    }

    private static async Task<List<LeagueSummary>> List(HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var user = http.GetCurrentUser();
        var season = StockholmTime.Today(time).Year;
        var leagues = await db.Leagues.Include(l => l.Members)
            .Where(l => l.Members.Any(m => m.UserId == user.Id))
            .OrderBy(l => l.Name)
            .AsNoTracking()
            .ToListAsync(http.RequestAborted);

        return leagues.Select(l =>
        {
            var me = l.Member(user.Id)!;
            return new LeagueSummary(l.Id, l.Name, l.CadenceFor(season), l.Tier, me.Role, me.Status, l.ActiveCount(), l.Capacity());
        }).ToList();
    }

    private static async Task<IResult> Join(
        JoinLeagueRequest request, HttpContext http, IskBattleDbContext db, TimeProvider time, INotificationService notifications)
    {
        var code = InviteCode.Normalize(request.InviteCode);
        var league = code is null
            ? null
            : await db.Leagues.Include(l => l.Members).SingleOrDefaultAsync(l => l.InviteCode == code, http.RequestAborted);
        if (league is null)
            return Problems.NotFound("invite_not_found", "No league with that invite code.");

        var user = http.GetCurrentUser();
        var outcome = MembershipService.Join(league, user.Id, time.GetUtcNow());
        if (outcome != JoinOutcome.AlreadyMember && !await db.TrySaveLeagueAsync(league, http.RequestAborted))
            return Problems.Conflict();

        if (outcome == JoinOutcome.Pending)
            await notifications.JoinPendingAsync(league, user, http.RequestAborted);

        var status = league.Member(user.Id)!.Status;
        return Results.Ok(new JoinLeagueResponse(league.Id, league.Name, status, outcome == JoinOutcome.AlreadyMember));
    }

    private static async Task<LeagueDetail> Get(HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var (league, me) = http.GetLeague();
        return await DetailAsync(db, league, me, time, http.RequestAborted);
    }

    private static async Task<IResult> Update(UpdateLeagueRequest request, HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var (league, me) = http.GetLeague();
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (name.Length is 0 or > MaxNameLength)
                return Problems.BadRequest("league_name_invalid", $"League name must be 1–{MaxNameLength} characters.");
            league.Name = name;
        }
        if (request.NextSeasonCadence is { } cadence)
        {
            if (!Enum.IsDefined(cadence))
                return Problems.BadRequest("cadence_invalid", "Unknown cadence.");
            // Sending the current cadence cancels a queued change.
            league.QueueCadence(cadence, StockholmTime.Today(time).Year);
        }

        if (!await db.TrySaveLeagueAsync(league, http.RequestAborted))
            return Problems.Conflict();
        return Results.Ok(await DetailAsync(db, league, me, time, http.RequestAborted));
    }

    private static async Task<IResult> Delete(HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var (league, _) = http.GetLeague();
        league.DeletedAt = time.GetUtcNow();
        await db.SaveChangesAsync(http.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> RegenerateInviteCode(HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var (league, me) = http.GetLeague();
        league.InviteCode = await UnusedInviteCodeAsync(db, http.RequestAborted);
        if (!await db.TrySaveLeagueAsync(league, http.RequestAborted))
            return Problems.Conflict();
        return Results.Ok(await DetailAsync(db, league, me, time, http.RequestAborted));
    }

    private static async Task<IResult> RemoveMember(
        Guid userId, HttpContext http, IskBattleDbContext db, TimeProvider time, INotificationService notifications)
    {
        var (league, me) = http.GetLeague();
        var target = league.Member(userId);
        if (target is null)
            return Problems.NotFound("member_not_found", "Not a member of this league.");
        if (!MembershipService.CanRemove(me, target))
            return Problems.Forbidden("league_forbidden", "You can't remove this member.");

        var activated = MembershipService.Remove(league, userId, time.GetUtcNow());
        if (!await db.TrySaveLeagueAsync(league, http.RequestAborted))
            return Problems.Conflict();

        if (activated.Count > 0)
            await notifications.MembersActivatedAsync(league, [.. activated.Select(m => m.UserId)], http.RequestAborted);
        return Results.NoContent();
    }

    private static async Task<IResult> Standings(string? period, DateOnly? date, HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var (league, me) = http.GetLeague();
        var day = date ?? StockholmTime.Today(time);
        if (!Enum.TryParse<PeriodType>(period ?? nameof(PeriodType.Year), ignoreCase: true, out var type)
            || !LeagueRules.VisiblePeriods(league.CadenceFor(day.Year)).Contains(type))
        {
            return Problems.BadRequest("period_invalid", "That period isn't shown for this league.");
        }

        var selected = PeriodCalculator.For(type, day);
        var activeIds = league.Members.Where(m => m.Status == MemberStatus.Active).Select(m => m.UserId).ToList();
        var members = await db.Users
            .Where(u => activeIds.Contains(u.Id))
            .Select(u => new StandingsMember(u.Id, u.DisplayName))
            .ToListAsync(http.RequestAborted);
        var checkIns = await db.CheckIns.ForStandingsAsync(activeIds, selected, http.RequestAborted);

        var rows = StandingsCalculator.Calculate(selected, members, checkIns)
            .Select(r => new StandingsRowResponse(
                r.UserId, r.DisplayName, r.Status, r.Rank, r.PeriodReturn, r.YtdPercent, r.UserId == me.UserId))
            .ToList();
        return Results.Ok(new StandingsResponse(ToResponse(selected), rows));
    }

    private static PeriodResponse ToResponse(Period period) => new(
        period.Type,
        period.Start,
        period.End,
        period.Season,
        period.Type == PeriodType.Week ? ISOWeek.GetWeekOfYear(period.Start.ToDateTime(TimeOnly.MinValue)) : null);

    private static async Task<LeagueDetail> DetailAsync(
        IskBattleDbContext db, League league, LeagueMember me, TimeProvider time, CancellationToken ct)
    {
        var season = StockholmTime.Today(time).Year;
        var isHost = me.IsHost();
        var visible = league.Members
            .Where(m => me.Status == MemberStatus.Active && (m.Status == MemberStatus.Active || isHost))
            .ToList();
        var visibleIds = visible.Select(m => m.UserId).ToList();
        var names = await db.Users
            .Where(u => visibleIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return new LeagueDetail(
            league.Id,
            league.Name,
            league.CadenceFor(season),
            league.CadenceFor(season + 1) is var next && next != league.CadenceFor(season) ? next : null,
            league.Tier,
            league.Capacity(),
            league.ActiveCount(),
            league.Members.Count(m => m.Status == MemberStatus.Pending),
            me.Role,
            me.Status,
            isHost ? league.InviteCode : null,
            LeagueRules.VisiblePeriods(league.CadenceFor(season)),
            visible
                .OrderBy(m => m.Status)
                .ThenBy(m => m.JoinedAt)
                .Select(m => new LeagueMemberResponse(m.UserId, names.GetValueOrDefault(m.UserId, ""), m.Role, m.Status))
                .ToList(),
            league.UpgradeOptions().Select(o => new UpgradeOption(o.Tier, o.Capacity, o.PriceOre)).ToList());
    }

    private static async Task<string> UnusedInviteCodeAsync(IskBattleDbContext db, CancellationToken ct)
    {
        while (true)
        {
            var code = InviteCode.New();
            if (!await db.Leagues.IgnoreQueryFilters().AnyAsync(l => l.InviteCode == code, ct))
                return code;
        }
    }
}
