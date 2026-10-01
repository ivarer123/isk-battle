namespace IskBattle.Domain.Tests;

public class GameConfigTests
{
    [Theory]
    [InlineData(LeagueTier.Free, LeagueTier.Premium50, 9_900)]
    [InlineData(LeagueTier.Premium50, LeagueTier.Premium100, 10_000)]
    [InlineData(LeagueTier.Premium100, LeagueTier.Premium250, 20_000)]
    [InlineData(LeagueTier.Free, LeagueTier.Premium250, 39_900)]
    public void Upgrade_costs_the_difference(LeagueTier from, LeagueTier to, int expectedOre) =>
        Assert.Equal(expectedOre, GameConfig.UpgradePriceOre(from, to));
}
