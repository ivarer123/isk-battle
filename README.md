# ISK Battle

Season-long YTD competition between friends. Rules: [GAME-DESIGN.md](GAME-DESIGN.md). Technical design: [ARCHITECTURE.md](ARCHITECTURE.md).

## Development

Requirements: .NET 10 SDK, Docker (for SQL Server and the integration tests).

```sh
dotnet tool restore
dotnet build
dotnet test
```

`tests/IskBattle.Api.Tests` starts SQL Server through Testcontainers. If your Docker engine is older than API 1.44
(Docker Desktop < 4.27), run the tests with `DOCKER_API_VERSION=1.43 dotnet test` or update Docker Desktop.

### Database migrations

```sh
dotnet ef migrations add <Name> -p src/IskBattle.Infrastructure -o Persistence/Migrations
```
