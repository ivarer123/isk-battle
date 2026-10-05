using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IskBattle.Api.Auth;
using IskBattle.Api.Endpoints;
using IskBattle.Infrastructure;
using IskBattle.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Build-time OpenAPI generation (GetDocument.Insider) runs this file without deployment settings.
var generatingOpenApi = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
builder.Services.AddInfrastructure(builder.Configuration, validateOnStart: !generatingOpenApi);
if (builder.Environment.IsDevelopment())
    builder.Services.AddDevClock(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

// Microsoft Entra External ID (email one-time passcode). Keep raw claim names ("oid", "email").
var devAuth = builder.Configuration.GetValue<bool>("Auth:DevMode");
if (devAuth && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Auth:DevMode is only allowed in Development.");

var authentication = builder.Services
    .AddAuthentication(devAuth ? DevAuthHandler.SchemeName : JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"];
        o.Audience = builder.Configuration["Auth:Audience"];
        o.MapInboundClaims = false;
    });
if (devAuth)
    authentication.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (var policy in new[] { CheckInEndpoints.RateLimitPolicy, LeagueEndpoints.JoinRateLimitPolicy })
    {
        o.AddPolicy(policy, http => RateLimitPartition.GetFixedWindowLimiter(
            http.User.FindFirst(CurrentUser.IdClaim)?.Value ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    }
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IskBattleDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapUnsubscribeEndpoints();
app.MapStripeWebhook();

var api = app.MapGroup("").RequireAuthorization().AddEndpointFilter(CurrentUser.UpsertFilter);
api.MapMeEndpoints();
api.MapCheckInEndpoints();
api.MapLeagueEndpoints();
api.MapCheckoutEndpoint();

app.Run();

public partial class Program;
