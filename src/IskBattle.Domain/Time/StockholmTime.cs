namespace IskBattle.Domain.Time;

/// <summary>All business time is Europe/Stockholm. Converts injected UTC time at the domain boundary.</summary>
public static class StockholmTime
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(GameConfig.TimeZoneId);

    public static DateTimeOffset Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone);

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(Now(time).DateTime);

    /// <summary>A Stockholm wall-clock time as an absolute instant.</summary>
    public static DateTimeOffset ToOffset(DateTime stockholmLocal) =>
        new(stockholmLocal, Zone.GetUtcOffset(stockholmLocal));
}
