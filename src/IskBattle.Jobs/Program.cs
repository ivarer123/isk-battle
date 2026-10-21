using Azure.Monitor.OpenTelemetry.Exporter;
using IskBattle.Infrastructure;
using IskBattle.Infrastructure.Persistence;
using IskBattle.Infrastructure.Reminders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Jobs (ARCHITECTURE.md §8, §10), run as Container Apps Jobs from the API's image:
//   dotnet /app/jobs/IskBattle.Jobs.dll reminders   scheduled check-in reminders
//   dotnet /app/jobs/IskBattle.Jobs.dll migrate     applies EF Core migrations (started by the deploy workflow)
// A failed run exits with code 1, which makes Container Apps retry it.
if (args is not [("reminders" or "migrate") and var command, ..])
{
    await Console.Error.WriteLineAsync("Usage: IskBattle.Jobs reminders|migrate");
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args[1..],
    // The image also holds the API with its own appsettings.json; read this app's files, not the working directory's.
    ContentRootPath = AppContext.BaseDirectory,
});
// Migrations need only the database, so they don't require the email and link settings.
builder.Services.AddInfrastructure(builder.Configuration, validateOnStart: command == "reminders");
// Application Insights in Azure; the connection string is set on the Container Apps Job only.
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddOpenTelemetry().UseAzureMonitorExporter();
if (builder.Environment.IsDevelopment())
    builder.Services.AddDevClock(builder.Configuration);

using var host = builder.Build();
try
{
    await host.StartAsync(); // runs startup validation of the email and link settings
    await using (var scope = host.Services.CreateAsyncScope())
    {
        if (command == "migrate")
            await scope.ServiceProvider.GetRequiredService<IskBattleDbContext>().Database.MigrateAsync();
        else
            await scope.ServiceProvider.GetRequiredService<ReminderJob>().RunAsync();
    }
    await host.StopAsync();
    return 0;
}
catch (Exception exception)
{
    // Returning instead of rethrowing: an unhandled exception here leaves the host's console lifetime waiting,
    // and the process never exits.
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("IskBattle.Jobs").LogCritical(exception, "Job failed");
    return 1;
}
