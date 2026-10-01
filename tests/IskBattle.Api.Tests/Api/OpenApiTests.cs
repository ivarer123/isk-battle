using IskBattle.Api.Tests.Infrastructure;

namespace IskBattle.Api.Tests.Api;

public class OpenApiTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    [Fact]
    public async Task Document_describes_the_api()
    {
        var document = await Factory.CreateClient().GetStringAsync("/openapi/v1.json");

        Assert.Contains("\"/leagues/{id}/standings\"", document);
        Assert.Contains("\"/check-ins\"", document);
    }
}
