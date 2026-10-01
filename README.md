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

### Run locally

```sh
docker run -d --name isk-battle-sql -p 1433:1433 -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Local!Passw0rd' \
  mcr.microsoft.com/mssql/server:2022-latest
dotnet run --project src/IskBattle.Api --launch-profile http   # http://localhost:5080, migrates on start
cd src/web && npm install && npm run dev                        # http://localhost:5173, proxies /api
```

In Development the API accepts a dev sign-in (`Auth:DevMode`): the web app asks for a name and sends it as
`X-Dev-User`. Open the site in two browsers (or a private window) to play as two players. The API refuses to
start with DevMode outside Development.

The API build regenerates `src/web/src/api/openapi.json`; run `npm run generate:api` in `src/web` to update the
typed client.

### Database migrations

```sh
dotnet ef migrations add <Name> -p src/IskBattle.Infrastructure -o Persistence/Migrations
```
