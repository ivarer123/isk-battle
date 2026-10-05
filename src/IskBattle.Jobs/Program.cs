using IskBattle.Infrastructure;
using IskBattle.Infrastructure.Reminders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Scheduled jobs (ARCHITECTURE.md §8), run as a Container Apps Job from the API's image:
//   dotnet /app/jobs/IskBattle.Jobs.dll reminders
// A failed run exits with code 1, which makes Container Apps retry it.
if (args is not ["reminders", ..])
{
    await Console.Error.WriteLineAsync("Usage: IskBattle.Jobs reminders");
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args[1..],
    // The image also holds the API with its own appsettings.json; read this app's files, not the working directory's.
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddInfrastructure(builder.Configuration);
if (builder.Environment.IsDevelopment())
    builder.Services.AddDevClock(builder.Configuration);

using var host = builder.Build();
try
{
    await host.StartAsync(); // runs startup validation of the email and link settings
    await using (var scope = host.Services.CreateAsyncScope())
        await scope.ServiceProvider.GetRequiredService<ReminderJob>().RunAsync();
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
