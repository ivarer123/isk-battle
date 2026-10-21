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

Run the reminder job once (it exits without touching the database unless it is 08:00–08:59 Stockholm, or
20:00–20:59 on the year's last trading day; reminders are printed to the console in Development):

```sh
DOTNET_ENVIRONMENT=Development Dev__Now=2026-10-17T08:30:00+02:00 dotnet run --project src/IskBattle.Jobs -- reminders
```

### Container image

```sh
docker build -t isk-battle .                                                              # API + jobs
docker run --entrypoint dotnet isk-battle /app/jobs/IskBattle.Jobs.dll reminders          # reminder job
docker run --entrypoint dotnet isk-battle /app/jobs/IskBattle.Jobs.dll migrate            # migrations
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

## Deploy (Azure)

One environment (`rg-iskbattle`, Sweden Central), ARCHITECTURE.md §10. Infrastructure is deployed from a developer
machine with Bicep; pushes to `main` roll out new images via `.github/workflows/deploy.yml` after CI passes.

**Always pin the subscription** – the CLI may also see other (work) subscriptions:

```sh
export SUB=<personal subscription id>
export ISK_ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
export ISK_ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
export ISK_GITHUB_REPOSITORY=<owner>/<repo>
export ISK_BUDGET_EMAIL=<your email>
deploy() { az deployment sub create --subscription "$SUB" --location swedencentral --name iskbattle \
  --template-file infra/main.bicep --parameters infra/main.bicepparam "$@"; }
```

### First-time setup

1. **Providers** (once): `for ns in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights Microsoft.Sql
   Microsoft.KeyVault Microsoft.Communication Microsoft.Web Microsoft.ManagedIdentity; do az provider register
   --subscription "$SUB" -n $ns; done`
2. **Pass 1 – shared resources:** `deploy` (no apps yet). Check first with `az deployment sub what-if …`.
3. **SQL access for the app identity:** in the portal, SQL database `iskbattle` → Query editor (sign in with Entra):
   ```sql
   CREATE USER [id-iskbattle] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [id-iskbattle];
   ALTER ROLE db_datawriter ADD MEMBER [id-iskbattle];
   ALTER ROLE db_ddladmin ADD MEMBER [id-iskbattle];
   ```
4. **Key Vault secrets** (`kv-iskbattle-…`, output `keyVaultName`); never paste them into chats or files:
   ```sh
   az keyvault secret set --subscription "$SUB" --vault-name $KV -n Email--UnsubscribeKey --value "$(openssl rand -base64 48)" -o none
   ```
   `Stripe--SecretKey` (`sk_test_…`) and `Stripe--WebhookSecret` (`whsec_…`): add them in the portal (Key Vault →
   Secrets). Until Stripe is set up, any placeholder value works (checkout then fails, nothing else).
5. **Entra External ID** (external tenant, email one-time passcode user flow):
   - App registration **ISK Battle API**: Expose an API → Application ID URI `api://<api-client-id>`, scope
     `access_as_user`; Token configuration → optional claims for the **access** token: `email`. Manifest
     `"requestedAccessTokenVersion": 2`.
   - App registration **ISK Battle web**: platform *Single-page application*, redirect URIs = the app URL and
     `http://localhost:5173`; API permissions → `access_as_user` (grant admin consent).
   - User flow → Applications → add *ISK Battle web*.
6. **Pass 2 – apps:** `ISK_DEPLOY_APPS=true ISK_AUTH_AUTHORITY=https://<subdomain>.ciamlogin.com/<tenant-id>/v2.0
   ISK_AUTH_AUDIENCE=<api-client-id> deploy`. On every later infrastructure deploy also pass the running image:
   `ISK_IMAGE=$(az containerapp show --subscription "$SUB" -g rg-iskbattle -n ca-iskbattle-api --query
   "properties.template.containers[0].image" -o tsv)`, otherwise the apps are reset to the placeholder image.
7. **GitHub** (repo settings → Secrets and variables → Actions):
   - Variables: `AZURE_CLIENT_ID` (output `githubDeployClientId`), `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
     `VITE_API_BASE_URL` (output `apiUrl`), `VITE_ENTRA_CLIENT_ID` (web app registration),
     `VITE_ENTRA_AUTHORITY` (`https://<subdomain>.ciamlogin.com/`), `VITE_API_SCOPE`
     (`api://<api-client-id>/access_as_user`).
   - Secret: `SWA_DEPLOYMENT_TOKEN` = `az staticwebapp secrets list --subscription "$SUB" -g rg-iskbattle -n
     swa-iskbattle --query properties.apiKey -o tsv`.
   - After the first workflow run: Packages → `isk-battle` → visibility **Public** (Container Apps pulls without
     credentials).
8. **Stripe webhook** (test mode): endpoint `<apiUrl>/webhooks/stripe` with the events listed under Payments; put
   its signing secret in `Stripe--WebhookSecret`, then restart the API revision so it re-reads secrets.

### Before launch (milestone 9)

SQL free offer: set "behavior when free limit reached" to *continue using the database*
(`freeLimitExhaustionBehavior: BillOverUsage`). Custom domains for the app and the API (pass the app origin in
`extraAppOrigins`), ACS custom email domain with SPF/DKIM/DMARC, Stripe live keys.
