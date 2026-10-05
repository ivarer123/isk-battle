using IskBattle.Api.Tests.Api;
using IskBattle.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Tests.Email;

/// <summary>Missing email or link settings stop the app at startup instead of failing requests later.</summary>
public class StartupValidationTests(SqlServerFixture sql) : ApiTestBase(sql)
{
    [Theory]
    [InlineData("Email:UnsubscribeKey", "too-short")]
    [InlineData("Email:Provider", "Acs")]          // no sender address or ACS endpoint configured
    [InlineData("Email:Provider", "Smtp")]
    [InlineData("Links:ApiBaseUrl", "")]
    [InlineData("Links:AppBaseUrl", "app.example.se")]
    public void Invalid_settings_fail_startup(string key, string value)
    {
        using var factory = Factory.WithWebHostBuilder(b => b.UseSetting(key, value));

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.IsType<OptionsValidationException>(exception.GetBaseException(), exactMatch: false);
    }

    [Fact]
    public void Acs_with_endpoint_and_sender_starts()
    {
        using var factory = Factory.WithWebHostBuilder(b => b
            .UseSetting("Email:Provider", "Acs")
            .UseSetting("Email:SenderAddress", "noreply@example.se")
            .UseSetting("Email:AcsEndpoint", "https://acs.example.communication.azure.com"));

        using var client = factory.CreateClient();
    }
}
