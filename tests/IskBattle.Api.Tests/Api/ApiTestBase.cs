using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Domain;

namespace IskBattle.Api.Tests.Api;

public abstract class ApiTestBase(SqlServerFixture sql) : IAsyncDisposable
{
    protected ApiFactory Factory { get; } = new(sql);

    protected SqlServerFixture Sql { get; } = sql;

    protected async Task<LeagueDetail> CreateLeagueAsync(HttpClient owner, Cadence cadence = Cadence.Weekly, string name = "Kompisligan") =>
        await (await owner.PostJsonAsync("/leagues", new { name, cadence })).ReadAsync<LeagueDetail>();

    protected async Task<HttpClient> JoinedPlayerAsync(string name, LeagueDetail league, MemberStatus expected = MemberStatus.Active)
    {
        var player = await Factory.NewPlayerAsync(name);
        var joined = await (await player.PostJsonAsync("/leagues/join", new { inviteCode = league.InviteCode })).ReadAsync<JoinLeagueResponse>();
        Assert.Equal(expected, joined.Status);
        return player;
    }

    protected static async Task CheckInAsync(HttpClient player, decimal ytd) =>
        await (await player.PostJsonAsync("/check-ins", new { ytdPercent = ytd, confirmed = true })).ReadAsync<CheckInResponse>();

    /// <summary>Moves the clock to 18:00 Stockholm (CEST) on the given date.</summary>
    protected void SetDate(int year, int month, int day) =>
        Factory.Time.SetUtcNow(new DateTimeOffset(year, month, day, 16, 0, 0, TimeSpan.Zero));

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
