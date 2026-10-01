using IskBattle.Domain.Entities;

namespace IskBattle.Domain.Tests;

internal static class TestData
{
    public static DateOnly D(int year, int month, int day) => new(year, month, day);

    public static CheckIn CheckIn(Guid userId, DateOnly forDate, decimal ytd, DateTimeOffset? recordedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ForDate = forDate,
        YtdPercent = ytd,
        RecordedAt = recordedAt ?? new DateTimeOffset(forDate.ToDateTime(new TimeOnly(18, 0)), TimeSpan.Zero),
    };
}
