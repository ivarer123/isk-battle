using IskBattle.Domain.Entities;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Api.Auth;

internal static class CurrentUser
{
    private const string ItemKey = "IskBattle.CurrentUser";

    public static User GetCurrentUser(this HttpContext http) => (User)http.Items[ItemKey]!;

    /// <summary>
    /// Upserts the <see cref="User"/> from token claims on every authenticated request (ARCHITECTURE.md §6).
    /// </summary>
    public static async ValueTask<object?> UpsertFilter(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var externalId = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(externalId))
            return Results.Unauthorized();

        var db = http.RequestServices.GetRequiredService<IskBattleDbContext>();
        var user = await db.Users.SingleOrDefaultAsync(u => u.ExternalId == externalId, http.RequestAborted)
                   ?? await CreateAsync(http, db, externalId);

        var email = http.User.FindFirst("email")?.Value;
        if (!string.IsNullOrEmpty(email) && email != user.Email)
        {
            user.Email = email;
            await db.SaveChangesAsync(http.RequestAborted);
        }

        http.Items[ItemKey] = user;
        return await next(context);
    }

    private static async Task<User> CreateAsync(HttpContext http, IskBattleDbContext db, string externalId)
    {
        var time = http.RequestServices.GetRequiredService<TimeProvider>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            Email = http.User.FindFirst("email")?.Value ?? "",
            DisplayName = "", // set during onboarding
            CreatedAt = time.GetUtcNow(),
        };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(http.RequestAborted);
            return user;
        }
        catch (DbUpdateException)
        {
            // A parallel first request created the user.
            db.Entry(user).State = EntityState.Detached;
            return await db.Users.SingleAsync(u => u.ExternalId == externalId, http.RequestAborted);
        }
    }
}
