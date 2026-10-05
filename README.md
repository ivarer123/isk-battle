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

To try check-in windows and reminders at another time, start the API with a fake clock (Development only; the
clock keeps running from that instant):

```sh
Dev__Now=2026-10-06T08:30:00+02:00 dotnet run --project src/IskBattle.Api --launch-profile http
```

The API build regenerates `src/web/src/api/openapi.json`; run `npm run generate:api` in `src/web` to update the
typed client.

### Payments (Stripe test mode)

Without Stripe keys the upgrade button returns "Betalning är inte tillgänglig just nu" (503). To test payments:

1. In the Stripe Dashboard (test mode): enable **Card** and **Swish** under Settings → Payment methods, and turn on
   "Successful payments" customer emails (Stripe sends the receipts).
2. Store the keys as user secrets:
   ```sh
   dotnet user-secrets --project src/IskBattle.Api set Stripe:SecretKey sk_test_...
   stripe listen --forward-to localhost:5080/webhooks/stripe   # prints whsec_...
   dotnet user-secrets --project src/IskBattle.Api set Stripe:WebhookSecret whsec_...
   ```
3. Pay with test card `4242 4242 4242 4242`. Two players buying the same tier at once leaves the second purchase as
   `NeedsRefund` (logged as a warning); refund it manually in the Stripe Dashboard.

For production, create the webhook endpoint for `checkout.session.completed`,
`checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed` and `checkout.session.expired`,
using the API version of the Stripe.net SDK in `Directory.Packages.props`.

### Database migrations

```sh
dotnet ef migrations add <Name> -p src/IskBattle.Infrastructure -o Persistence/Migrations
```
