using System.Net;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Endpoints;

/// <summary>
/// Unsubscribe from email links, no sign-in needed (ARCHITECTURE.md §6). GET only shows a confirm page,
/// because mail scanners (Safe Links, prefetching) open links in emails; the change happens on POST, which
/// serves both the page's button and mail clients using RFC 8058 List-Unsubscribe-Post.
/// </summary>
internal static class UnsubscribeEndpoints
{
    // Keeps å, ä, ö readable instead of numeric entities.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    public static void MapUnsubscribeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/unsubscribe", Confirm).AllowAnonymous().ExcludeFromDescription();
        app.MapPost("/unsubscribe", Unsubscribe).AllowAnonymous().ExcludeFromDescription().DisableAntiforgery();
    }

    private static IResult Confirm(string? token, UnsubscribeTokens tokens, IOptions<AppLinkOptions> links)
    {
        if (tokens.Validate(token) is null)
            return InvalidLink(links.Value);

        var action = Html.Encode($"?token={Uri.EscapeDataString(token!)}");
        return Page(HttpStatusCode.OK, "Sluta få mejl?",
            "Du slutar få påminnelser och andra mejl från ISK Battle. Du kan slå på dem igen under Profil i appen.",
            links.Value,
            $"""<form method="post" action="{action}"><button type="submit" style="font:inherit;padding:.6rem 1.2rem">Ja, stäng av mejl</button></form>""");
    }

    private static async Task<IResult> Unsubscribe(
        string? token, UnsubscribeTokens tokens, IskBattleDbContext db, IOptions<AppLinkOptions> links, CancellationToken ct)
    {
        var userId = tokens.Validate(token);
        if (userId is not { } id)
            return InvalidLink(links.Value);

        // A deleted account updates nothing, which is also a success for the reader.
        await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailRemindersEnabled, false), ct);
        return Page(HttpStatusCode.OK, "Klart!",
            "Du får inga fler mejl från ISK Battle. Vill du ha påminnelser igen kan du slå på dem under Profil i appen.", links.Value);
    }

    private static IResult InvalidLink(AppLinkOptions links) =>
        Page(HttpStatusCode.BadRequest, "Länken fungerar inte", "Länken är ogiltig. Du kan stänga av e-post under Profil i appen.", links);

    /// <param name="trustedHtml">Extra markup built by this class; never user input.</param>
    private static IResult Page(HttpStatusCode status, string title, string text, AppLinkOptions links, string trustedHtml = "")
    {
        var app = Html.Encode(links.AppBaseUrl);
        var html = $"""
            <!doctype html>
            <html lang="sv"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>{Html.Encode(title)} – ISK Battle</title></head>
            <body style="font-family:system-ui,sans-serif;max-width:32rem;margin:3rem auto;padding:0 1rem;color:#1c1917">
            <h1>{Html.Encode(title)}</h1><p>{Html.Encode(text)}</p>{trustedHtml}<p><a href="{app}">Till ISK Battle</a></p>
            </body></html>
            """;
        return Results.Content(html, "text/html; charset=utf-8", statusCode: (int)status);
    }
}
