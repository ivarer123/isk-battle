using System.Text.Encodings.Web;
using System.Text.Unicode;

namespace IskBattle.Infrastructure.Email;

/// <summary>Swedish plain HTML + text templates (ARCHITECTURE.md §8). All values are HTML-encoded.</summary>
public static class EmailTemplates
{
    // Keeps å, ä, ö readable instead of numeric entities.
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    public static EmailMessage CheckInReminder(string to, string displayName, string appUrl, string unsubscribeUrl) =>
        Build(to, unsubscribeUrl,
            subject: "Dags att uppdatera din utveckling",
            greeting: $"Hej {displayName}!",
            paragraphs:
            [
                "Det är dags att rapportera din utveckling i år. Öppna ISK Battle och ange siffran från kontoöversikten hos din bank eller mäklare.",
                "Ingen fara om du missar – du visas som Pausad och är tillbaka i tabellen nästa gång du rapporterar.",
            ],
            button: ("Uppdatera nu", appUrl));

    public static EmailMessage JoinPending(string to, string joinerName, string leagueName, string upgradeUrl, string unsubscribeUrl) =>
        Build(to, unsubscribeUrl,
            subject: $"{joinerName} vill gå med i {leagueName}",
            greeting: "Hej!",
            paragraphs: [$"{joinerName} vill gå med i {leagueName}. Uppgradera för att släppa in fler."],
            button: ("Uppgradera ligan", upgradeUrl));

    public static EmailMessage MemberActivated(string to, string displayName, string leagueName, string leagueUrl, string unsubscribeUrl) =>
        Build(to, unsubscribeUrl,
            subject: $"Du är med i {leagueName}!",
            greeting: $"Hej {displayName}!",
            paragraphs: [$"Det finns plats nu – du är med i {leagueName}. Rapportera din utveckling så syns du i tabellen."],
            button: ("Till ligan", leagueUrl));

    private static EmailMessage Build(
        string to, string unsubscribeUrl, string subject, string greeting, string[] paragraphs, (string Text, string Url) button)
    {
        const string footer = "Du får det här mejlet eftersom du har ett konto på ISK Battle.";
        string E(string value) => Html.Encode(value);

        var html = $"""
            <!doctype html>
            <html lang="sv">
            <body style="margin:0;padding:24px;background:#f7f7f4;font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;color:#1c1917">
              <div style="max-width:480px;margin:0 auto;background:#ffffff;border-radius:12px;padding:24px">
                <p style="font-weight:700;color:#14532d;margin:0 0 16px">ISK Battle</p>
                <p>{E(greeting)}</p>
                {string.Concat(paragraphs.Select(p => $"<p>{E(p)}</p>"))}
                <p style="margin:24px 0"><a href="{E(button.Url)}" style="background:#14532d;color:#ffffff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:600">{E(button.Text)}</a></p>
                <p style="font-size:12px;color:#6b6b66">{E(footer)} <a href="{E(unsubscribeUrl)}" style="color:#6b6b66">Avsluta e-post</a></p>
              </div>
            </body>
            </html>
            """;

        var text = $"""
            {greeting}

            {string.Join("\n\n", paragraphs)}

            {button.Text}: {button.Url}

            --
            {footer}
            Avsluta e-post: {unsubscribeUrl}
            """;

        return new EmailMessage(to, subject, html, text, unsubscribeUrl);
    }
}
