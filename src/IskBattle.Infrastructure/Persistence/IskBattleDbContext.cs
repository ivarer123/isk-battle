using IskBattle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IskBattle.Infrastructure.Persistence;

public class IskBattleDbContext(DbContextOptions<IskBattleDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueMember> LeagueMembers => Set<LeagueMember>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<JoinPendingNotice> JoinPendingNotices => Set<JoinPendingNotice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IskBattleDbContext).Assembly);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as readable strings.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
