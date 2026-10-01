namespace IskBattle.Api.Tests.Infrastructure;

/// <summary>A settable clock that, like real time, moves forward a millisecond on every read.</summary>
public sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    private readonly Lock _lock = new();

    public void SetUtcNow(DateTimeOffset value)
    {
        lock (_lock) _now = value;
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock) return _now = _now.AddMilliseconds(1);
    }
}
