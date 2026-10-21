# ISK Battle – Claude's working notes

The user's side project: friends compare year-to-date (YTD) % of their main ISK account in season-long leagues.
Self-reported, trust-based, no prizes or penalties; nothing may encourage risk-taking. The user doesn't read this
file – it's for me. Keep it current and terse; history lives in git.

Sources of truth: **GAME-DESIGN.md** = rules (doc beats code; §4a windows, §6 standings, §12 decision log).
**ARCHITECTURE.md** = technical design (§10 hosting, §12 milestones). **README.md** = run/test/Stripe/**Deploy**.

## Rules for working with this user

- Never guess on spec ambiguity: ask (AskUserQuestion, recommended option first), then record the answer in
  GAME-DESIGN.md (clarification + §12 decision-log row) in the same commit.
- Small implementation choices: decide conservatively, list them in the summary so the user can veto.
- Commit per finished piece with passing tests; message ends with the Co-Authored-By line. Push to `origin` freely.
- Azure/GitHub actions that create or change things: show the commands and get an OK first (user approves in
  batches, e.g. "go for 1–3").
- UI Swedish, code/docs English. Percentages `decimal`, money integer öre, tunables only in `GameConfig`, time via
  injected `TimeProvider`, business time Europe/Stockholm.

## Accounts – never mix with work

- **GitHub:** personal `ivarer123`, repo `ivarer123/isk-battle` (private; GHCR package `isk-battle` is public).
  `gh` also holds the work account `forefront-romson_skistar` – check `gh api user --jq .login` = `ivarer123`
  before any `gh` call. Repo-local git uses `!gh auth git-credential` (active gh account).
- **Azure:** personal subscription `f15c93af-49fc-4a88-b904-693e0c7947c9`, tenant `3330bb49-…`
  (`ivarelfvingmail.onmicrosoft.com`). The same `az` login sees ~30 SkiStar subscriptions (tenant `a7624529-…`):
  **always pass `--subscription`**; never touch the work tenant. Admin object ID `8cebb3a7-93da-4a98-8bbb-571d7f96bac7`,
  login `ivar.elfvin_gmail.com#EXT#@ivarelfvingmail.onmicrosoft.com`. Secrets never go through chat or files.

## Where we are (2026-10-21) – resume here

Milestones 1–7 done; 8 (infrastructure) in progress; 9 (legal pages, launch) not started.
Agreed plan: one Azure environment that becomes prod at launch; until then Azure default URLs, Stripe **test
mode**, Azure-managed email sender. Domain, company (needed for Stripe live + Swish + legal footer) and live keys
only right before launch.

**Done in Azure** (Bicep pass 1, `rg-iskbattle`, Sweden Central, verified): Log Analytics + App Insights,
`id-iskbattle` (runtime), `id-iskbattle-github` (OIDC, client ID `46b38212-2b5f-4ecf-bdb7-103914cae7b9`,
Contributor on RG), Key Vault `kv-iskbattle-abqpsx`, SQL `sql-iskbattle-abqpsx.database.windows.net` / `iskbattle`
(free offer confirmed), ACS sender `DoNotReply@3ddcec5c-2868-4cf2-ae36-ace87b7ac2af.azurecomm.net`, Container Apps
env `cae-iskbattle`, SWA `swa-iskbattle` (East US 2 – West Europe closed to new customers) at
`https://lively-desert-0f78c2d0f.3.azurestaticapps.net`, budget 100/month. GitHub: CI green on GitHub; Deploy
workflow pushed the image to GHCR (its Azure/web jobs skip until repo variable `AZURE_CLIENT_ID` exists).

**Next, in order** (README "Deploy" has the exact commands; env vars `ISK_*` as listed there, budget email = user's):
1. *(user said "hold" 2026-10-21 – ask before doing)* SQL access for `id-iskbattle`: temporary firewall rule for
   the current IP, run the 4 T-SQL lines (CREATE USER … FROM EXTERNAL PROVIDER + datareader/datawriter/ddladmin)
   with a scratch `dotnet run` script using `Authentication=Active Directory Default`, remove the rule.
2. *(hold)* `Email--UnsubscribeKey` into Key Vault via `openssl rand -base64 48` (never printed).
3. Ask the user: Entra External ID tenant created? Stripe test account? If Stripe isn't ready, offer placeholder
   `Stripe--SecretKey`/`Stripe--WebhookSecret` so pass 2 can run.
4. Entra app registrations (user runs `! az login --tenant <external> --allow-no-subscriptions`): API (scope
   `access_as_user`, access-token optional claim `email`, v2 tokens) + SPA (redirects: app URL, localhost:5173),
   link SPA to the email-OTP user flow. **Without the `email` claim no email is ever sent.**
5. Pass 2: `ISK_DEPLOY_APPS=true ISK_AUTH_AUTHORITY=https://<sub>.ciamlogin.com/<tenant-id>/v2.0
   ISK_AUTH_AUDIENCE=<api-client-id>`; what-if first. Later infra redeploys must pass `ISK_IMAGE` = running image.
6. GitHub repo variables (`AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `VITE_API_BASE_URL`,
   `VITE_ENTRA_CLIENT_ID`, `VITE_ENTRA_AUTHORITY`, `VITE_API_SCOPE`) + secret `SWA_DEPLOYMENT_TOKEN`; rerun Deploy.
7. Stripe webhook `<apiUrl>/webhooks/stripe` → `Stripe--WebhookSecret`; restart API revision.
8. Verify end to end (sign-up → `/me` has email; check-in; test checkout 4242…; reminder email; App Insights;
   SQL auto-pauses; cost ~0). None of Entra/MSAL, Stripe checkout, ACS delivery, Container Apps Jobs, the API in
   Azure or the Deploy workflow's Azure/web jobs has ever run for real.

**Other open items:** the user's "minor UI points" (promised since 2026-10-05 – ask); draft Swedish terms +
privacy policy (data: email, display name, YTD %, purchases; 7-year bookkeeping retention; company details unknown
yet); at launch SQL `freeLimitExhaustionBehavior` → `BillOverUsage`, custom domains (`extraAppOrigins`), ACS
custom domain (SPF/DKIM/DMARC), Stripe live + Swish. GitHub Actions warns about Node 20 actions (bump
checkout/setup-node majors sometime).

## Solution map

```
src/IskBattle.Domain          BCL only. GameConfig, Periods/{PeriodCalculator,ExchangeHolidays},
                              Standings/StandingsCalculator (100 % branch coverage, CI gate), CheckIns/{CheckInWindows,
                              CheckInValidator}, Leagues/*, Reminders/ReminderPolicy, Purchases/PurchaseRules, Time/
src/IskBattle.Infrastructure  EF Core SQL Server + migrations, Email (Acs/Console senders, templates, SwedishDates,
                              UnsubscribeTokens, EmailLinks), Reminders/ReminderJob, Payments (Stripe),
                              DependencyInjection.AddInfrastructure(validateOnStart)
src/IskBattle.Api             Minimal API, Auth (Entra JWT; DevAuthHandler in Development), Endpoints/*. Build
                              writes OpenAPI to src/web/src/api/openapi.json. Azure Monitor when
                              APPLICATIONINSIGHTS_CONNECTION_STRING is set
src/IskBattle.Jobs            `reminders` | `migrate` (Container Apps Jobs, command override)
Dockerfile                    One image: /app/api (entrypoint, :8080) + /app/jobs. Run a job locally with
                              `--entrypoint dotnet <image> /app/jobs/IskBattle.Jobs.dll migrate`
infra/                        main.bicep (sub scope: RG, budget) → resources.bicep → apps.bicep (pass 2, API +
                              caj-reminders-morning/-seasonend + caj-migrate). main.bicepparam reads ISK_* env vars
.github/workflows             ci.yml; deploy.yml (after CI on main: GHCR → caj-migrate → API/jobs → SWA)
src/web                       Vite + React 19 + TS + React Router 7 + TanStack Query, PWA, openapi-fetch client,
                              strings in src/i18n/sv.json, staticwebapp.config.json (SPA fallback)
tests/                        Domain.Tests (pure); Api.Tests (Testcontainers SQL, WebApplicationFactory "Testing")
```

## Decisions easy to get wrong (full list in GAME-DESIGN.md)

- Check-ins belong to a **trading day** (Mon–Fri minus Nasdaq Stockholm holidays, rule-based). Windows open 22:30,
  close 09:00 next day (Daily), Sunday 23:59 (Weekly, on the week's last trading day), 09:00 next trading day
  (Monthly). Season ends on the last trading day before Nyårsafton (28–30 Dec), all cadences, closes at midnight.
  Union of the player's cadences; Daily if no active league.
- Reminders 08:00 the day after (20:00 at season end) from cron, at most once per window (claimed via conditional
  update), no polling catch-up. Email names the longest open window's close ("Rapportera senast …").
- Standings: periods clipped to season; Dag = trading day → next trading day; ties on 2-dp, ranking 1,1,3;
  Ranked → Ny → Pausad; only Active members.
- Payments: co-host = first payer; concurrent buys → `NeedsRefund` / `PaidNeedsPartialRefund` (manual refund);
  checkout 503 without Stripe keys.
- One email switch governs all email. Users keyed on Entra `oid`.

## Gotchas

- .NET tests: `DOCKER_API_VERSION=1.43 dotnet test` with Docker Desktop running (`open -a Docker`). MTP runner;
  filter with `-- --filter-class <FullName>`. TreatWarningsAsErrors + NuGet audit as errors (transitive pin of
  `System.Security.Cryptography.Pkcs` in Directory.Packages.props).
- After API contract changes: build the API, then `npm run generate:api`; CI fails on uncommitted generated files.
- API and job wrap the run in try/catch and return 1 – an unhandled exception after host start hangs the process.
  The API rethrows only under WebApplicationFactory (`Testing`, flag read **before** the try: the host is disposed
  by the catch).
- Local run: SQL container `isk-battle-sql` :1433, API :5080 `--launch-profile http`, Vite :5173 proxies `/api`.
  `Dev__Now=<ISO>` shifts the clock in Development. Local DB players: anna, bertil, cecilia, david ("Veckoligan"),
  `ivarer` ("hejs liga"), all Weekly. UI checks: Playwright from scratchpad with
  `~/Library/Caches/ms-playwright/chromium-1117` as executablePath. No `timeout` or `sqlcmd` on this Mac
  (use `docker exec isk-battle-sql /opt/mssql-tools18/bin/sqlcmd -C …` locally).
- Repo `nuget.config` clears sources (global config has an unreachable SkiStar feed).
