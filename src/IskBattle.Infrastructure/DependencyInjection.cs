using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Notifications;
using IskBattle.Infrastructure.Persistence;
using IskBattle.Infrastructure.Reminders;
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

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.Configure<AppLinkOptions>(configuration.GetSection(AppLinkOptions.Section));
        services.AddSingleton<UnsubscribeTokens>();
        services.AddSingleton<EmailLinks>();
        if (string.Equals(configuration[$"{EmailOptions.Section}:Provider"], "Acs", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailSender, AcsEmailSender>();
        else
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();

        services.AddScoped<INotificationService, EmailNotificationService>();
        services.AddScoped<ReminderJob>();
        return services;
    }
}
