using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Payments;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IskBattle.Api.Tests.Infrastructure;

public sealed class ApiFactory(SqlServerFixture sql) : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public const string TestUnsubscribeKey = "test-unsubscribe-key-0123456789abcdef";

    /// <summary>Monday 21 September 2026, 12:00 in Stockholm.</summary>
    public TestClock Time { get; } = new(new DateTimeOffset(2026, 9, 21, 10, 0, 0, TimeSpan.Zero));

    public RecordingEmailSender Emails { get; } = new();

    public FakePaymentGateway Payments { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Sql", sql.ConnectionString);
        builder.UseSetting("Email:Provider", "Console");
        builder.UseSetting("Email:UnsubscribeKey", TestUnsubscribeKey);
        builder.UseSetting("Links:AppBaseUrl", "https://app.test");
        builder.UseSetting("Links:ApiBaseUrl", "https://api.test");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Time);
            services.AddSingleton<IEmailSender>(Emails);
            services.AddSingleton<IPaymentGateway>(Payments);
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    /// <summary>A client authenticated as a new player who has completed onboarding.</summary>
    public async Task<HttpClient> NewPlayerAsync(string displayName)
    {
        var client = CreateClient();
        var sub = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubHeader, sub);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, $"{sub}@example.com");
        (await client.PatchAsJsonAsync("/me", new { displayName }, Json)).EnsureSuccessStatusCode();
        return client;
    }
}

internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string SubHeader = "X-Test-Sub";
    public const string EmailHeader = "X-Test-Email";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubHeader, out var sub))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new("oid", sub.ToString()) };
        if (Request.Headers.TryGetValue(EmailHeader, out var email))
            claims.Add(new Claim("email", email.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sent = [];

    public IReadOnlyList<EmailMessage> Sent
    {
        get { lock (_sent) return [.. _sent]; }
    }

    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (Fail)
            throw new InvalidOperationException("Simulated delivery failure.");
        lock (_sent) _sent.Add(message);
        return Task.CompletedTask;
    }
}

internal static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string url) => await (await client.GetAsync(url)).ReadAsync<T>();

    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var code) ? code.GetString() : null;

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PatchJsonAsync(this HttpClient client, string url, object body) =>
        client.PatchAsJsonAsync(url, body, ApiFactory.Json);
}
