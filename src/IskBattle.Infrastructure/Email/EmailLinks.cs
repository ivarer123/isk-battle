using Microsoft.Extensions.Options;

namespace IskBattle.Infrastructure.Email;

public sealed class EmailLinks(IOptions<AppLinkOptions> options, UnsubscribeTokens tokens)
{
    private readonly AppLinkOptions _links = options.Value;

    public string App() => _links.AppBaseUrl.TrimEnd('/') + "/";

    public string League(Guid leagueId) => $"{_links.AppBaseUrl.TrimEnd('/')}/liga/{leagueId}";

    public string Upgrade(Guid leagueId) => $"{League(leagueId)}/uppgradera";

    public string Unsubscribe(Guid userId) =>
        $"{_links.ApiBaseUrl.TrimEnd('/')}/unsubscribe?token={Uri.EscapeDataString(tokens.Create(userId))}";
}
