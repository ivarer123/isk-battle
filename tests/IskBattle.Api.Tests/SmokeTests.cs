namespace IskBattle.Api.Tests;

// Placeholder until milestone 4 adds WebApplicationFactory + Testcontainers tests.
public class SmokeTests
{
    [Fact]
    public void Api_assembly_loads() =>
        Assert.Equal("IskBattle.Api", typeof(Program).Assembly.GetName().Name);
}
