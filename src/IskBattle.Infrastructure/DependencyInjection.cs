using IskBattle.Infrastructure.Email;
using IskBattle.Infrastructure.Notifications;
using IskBattle.Infrastructure.Payments;
using IskBattle.Infrastructure.Persistence;
using IskBattle.Infrastructure.Reminders;
using IskBattle.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IskBattle.Infrastructure;

public static class DependencyInjection
{
    /// <param name="validateOnStart">
    /// Fail startup on missing email/link settings. Off only for build-time OpenAPI generation, which starts
    /// the host without any deployment configuration.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, bool validateOnStart = true)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<IskBattleDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("Sql"),
            sql => sql.EnableRetryOnFailure())); // tolerates the free-tier auto-pause wake-up

        var email = services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.Section));
        var links = services.AddOptions<AppLinkOptions>().Bind(configuration.GetSection(AppLinkOptions.Section));
        if (validateOnStart)
        {
            email.ValidateOnStart();
            links.ValidateOnStart();
            services.AddSingleton<IValidateOptions<EmailOptions>, EmailOptionsValidation>();
            services.AddSingleton<IValidateOptions<AppLinkOptions>, EmailOptionsValidation>();
        }
        services.AddSingleton<UnsubscribeTokens>();
        services.AddSingleton<EmailLinks>();
        if (string.Equals(configuration[$"{EmailOptions.Section}:Provider"], "Acs", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailSender, AcsEmailSender>();
        else
            services.AddSingleton<IEmailSender, ConsoleEmailSender>();

        services.AddScoped<INotificationService, EmailNotificationService>();

        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.Section));
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<ReminderJob>();
        return services;
    }

    /// <summary>Development only: replaces the clock when <c>Dev:Now</c> is set (see <see cref="DevClock"/>).</summary>
    public static IServiceCollection AddDevClock(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration["Dev:Now"] is { Length: > 0 } devNow)
            services.AddSingleton<TimeProvider>(new DevClock(DateTimeOffset.Parse(devNow, System.Globalization.CultureInfo.InvariantCulture)));
        return services;
    }
}
