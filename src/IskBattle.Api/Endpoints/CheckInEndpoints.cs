using IskBattle.Api.Auth;
using IskBattle.Domain.CheckIns;
using IskBattle.Domain.Entities;
using IskBattle.Domain.Time;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Endpoints;

internal static class CheckInEndpoints
{
    public const string RateLimitPolicy = "check-ins";

    public static void MapCheckInEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/check-ins", CheckIn).RequireRateLimiting(RateLimitPolicy);
        api.MapGet("/me/check-ins", History);
    }

    private static async Task<IResult> CheckIn(CheckInRequest request, HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var user = http.GetCurrentUser();
        if (request.YtdPercent != Math.Round(request.YtdPercent, 4))
            return Problems.BadRequest("ytd_too_precise", "Use at most four decimals.");

        // The validator needs today's check-in (if any) and the latest one before it.
        var recent = await db.CheckIns
            .Where(c => c.UserId == user.Id)
            .OrderByDescending(c => c.ForDate)
            .Take(2)
            .ToListAsync(http.RequestAborted);

        var validation = CheckInValidator.Validate(request.YtdPercent, recent, time, request.Confirmed);
        switch (validation.Outcome)
        {
            case CheckInOutcome.OutOfRange:
                return Problems.BadRequest("ytd_out_of_range", "YTD must be between -99.99 and 1000.");
            case CheckInOutcome.RequiresConfirmation:
                return Problems.Of(StatusCodes.Status422UnprocessableEntity, "requires_confirmation",
                    "The value differs a lot from your previous check-in.",
                    new Dictionary<string, object?>
                    {
                        ["requiresConfirmation"] = true,
                        ["previousYtdPercent"] = validation.PreviousYtdPercent,
                    });
        }

        var checkIn = recent.Find(c => c.ForDate == validation.ForDate);
        if (checkIn is null)
        {
            checkIn = new CheckIn { Id = Guid.NewGuid(), UserId = user.Id, ForDate = validation.ForDate };
            db.CheckIns.Add(checkIn);
        }
        checkIn.YtdPercent = request.YtdPercent;
        checkIn.RecordedAt = time.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(http.RequestAborted);
        }
        catch (DbUpdateException)
        {
            return Problems.Of(StatusCodes.Status409Conflict, "concurrent_check_in", "A check-in was saved at the same time. Try again.");
        }

        return Results.Ok(new CheckInResponse(checkIn.ForDate, checkIn.YtdPercent));
    }

    private static async Task<List<CheckInResponse>> History(int? season, HttpContext http, IskBattleDbContext db, TimeProvider time)
    {
        var user = http.GetCurrentUser();
        var year = season ?? StockholmTime.Today(time).Year;
        var from = new DateOnly(year, 1, 1);
        var to = from.AddYears(1);
        return await db.CheckIns
            .Where(c => c.UserId == user.Id && c.ForDate >= from && c.ForDate < to)
            .OrderBy(c => c.ForDate)
            .Select(c => new CheckInResponse(c.ForDate, c.YtdPercent))
            .ToListAsync(http.RequestAborted);
    }
}
