using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IskBattle.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> only; never connects.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IskBattleDbContext>
{
    public IskBattleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<IskBattleDbContext>()
            .UseSqlServer("Server=design-time;Database=IskBattle")
            .Options);
}
