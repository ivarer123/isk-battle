using IskBattle.Infrastructure.Notifications;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IskBattle.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<IskBattleDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Sql"),
            sql => sql.EnableRetryOnFailure())); // tolerates the free-tier auto-pause wake-up
        services.AddScoped<INotificationService, LoggingNotificationService>();
        return services;
    }
}
