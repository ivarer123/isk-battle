using IskBattle.Api.Tests.Infrastructure;
using IskBattle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace IskBattle.Api.Tests.Infrastructure;

/// <summary>One SQL Server container per test run, migrated once. Tests isolate themselves with fresh ids.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "IskBattle",
        }.ConnectionString;

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public IskBattleDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<IskBattleDbContext>().UseSqlServer(ConnectionString).Options);

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
