namespace IskBattle.Api.Auth;

/// <summary>
/// Development only: <c>Dev:Now</c> (e.g. "2026-10-05T22:45:00+02:00") shifts the app's clock to that instant at
/// startup and lets it run from there, to try check-in windows and reminders at other times of day.
/// </summary>
internal sealed class DevClock(DateTimeOffset start) : TimeProvider
{
    private readonly TimeSpan _shift = start - System.GetUtcNow();

    public override DateTimeOffset GetUtcNow() => System.GetUtcNow() + _shift;
}
