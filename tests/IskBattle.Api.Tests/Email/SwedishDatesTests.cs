using IskBattle.Infrastructure.Email;

namespace IskBattle.Api.Tests.Email;

public class SwedishDatesTests
{
    private static readonly DateOnly Saturday = new(2026, 10, 31);

    [Theory]
    [InlineData(2026, 10, 31, 9, 0, "idag kl. 09.00")]
    [InlineData(2026, 10, 31, 22, 30, "ikväll kl. 22.30")]
    [InlineData(2026, 11, 1, 0, 0, "ikväll kl. 23.59")]     // midnight is shown as 23.59 the day before
    [InlineData(2026, 11, 2, 0, 0, "imorgon kl. 23.59")]
    [InlineData(2026, 11, 2, 9, 0, "måndag 2 november kl. 09.00")]
    public void Relative(int y, int m, int d, int hour, int minute, string expected) =>
        Assert.Equal(expected, SwedishDates.Relative(new DateTime(y, m, d, hour, minute, 0), Saturday));
}
