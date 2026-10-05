# ISK Battle – context for Claude Code

Side project by the user. Friends compare year-to-date (YTD) development of their main ISK account in season-long
leagues. Self-reported, trust-based, no prizes, no penalties; nothing may encourage risk-taking.

- **GAME-DESIGN.md** owns the rules (source of truth – "if code and doc disagree, the doc wins"). Its §6
  "Clarifications" and §12 decision log hold every rule agreed with the user so far.
- **ARCHITECTURE.md** owns the technical design; §12 is the milestone build order.
- **README.md** has run/test/migration/Stripe instructions.

## How to work with this user

- **Never resolve spec ambiguity by guessing.** List ambiguities/contradictions and ask (AskUserQuestion with a
  recommended option first). Record answers in GAME-DESIGN.md (clarification + decision-log row) in the same commit.
- Small implementation-level choices may be made conservatively, but list them explicitly in the summary so the user
  can veto them.
- One commit per milestone, each ending with passing tests. Commit messages end with the Co-Authored-By line.
- Repo is **local only** (no remote). The `gh` CLI on this machine is a work account (`forefront-romson_skistar`) –
  do not create repos or push with it. The user will push to their personal GitHub.
- UI language Swedish, code/docs English. Percentages `decimal`, money integer öre, tunables only in `GameConfig`,
  time via injected `TimeProvider`, business time Europe/Stockholm.

## Status (as of 2026-10-05)

| Milestone | State |
|---|---|
| 1 Skeleton + CI | done (`7f0c5b8`) |
| 2 Domain | done (`3598ec8`) |
| 3 Persistence | done (`8f546bc`) |
| 4 API core | done (`7b918c0`) |
| 5 Frontend | done (`5026f46`) |
| 6 Email + reminders | done (`792cafc`) |
| 7 Payments (Stripe) | done (`df844bc`) |
| 8 Infrastructure (Bicep, deploy, domains) | **next – blocked on user** |
| 9 Legal pages + launch checklist | not started (legal pages are placeholders) |

A full code review (2026-10-05) was followed by a fix commit (overcharge refunds, startup config validation,
New Year reminder windows, pending-join email spam, oid, 400s for out-of-range dates, unsubscribe confirm page).

Agreed pace: build continuously, pause before Azure deployment. Tests at last run: 302 .NET + 23 web, all green;
StandingsCalculator 100 % branch coverage (CI gate).

**Never verified for real (no accounts yet):** Entra/MSAL sign-in, real Stripe checkout, ACS email delivery, the
reminder job host, the GitHub Actions workflow, and clicking through the UI in a browser (only API-through-proxy smoke
tested).

**Blocked on the user for milestone 8/launch:** Azure subscription, Entra External ID tenant, domain name, Stripe
account with Swish (needs company / organisationsnummer – also required for the legal footer).

## Solution map

```
src/IskBattle.Domain          BCL only. Entities, GameConfig, Periods/PeriodCalculator, Standings/StandingsCalculator,
                              CheckIns/CheckInValidator, Leagues/{LeagueRules,MembershipService,InviteCode},
                              Reminders/ReminderPolicy, Purchases/PurchaseRules, Time/StockholmTime
src/IskBattle.Infrastructure  EF Core (SQL Server) + migrations, CheckInQueries.ForStandingsAsync (two indexed
                              queries), Email (IEmailSender: Acs/Console, Swedish templates, HMAC UnsubscribeTokens,
                              EmailLinks), Notifications/EmailNotificationService, Reminders/ReminderJob,
                              Payments (IPaymentGateway, StripePaymentGateway), DependencyInjection.AddInfrastructure
src/IskBattle.Api             Minimal API. Auth/{CurrentUser upsert filter, DevAuthHandler}, Leagues/LeagueAccess
                              (endpoint-filter authorization, rowversion save), Endpoints/*, Contracts.cs, Problems.cs.
                              Build writes OpenAPI to src/web/src/api/openapi.json
src/IskBattle.Jobs            Console (generic host): `reminders` -> ReminderJob; Container Apps Job on UTC cron
                              0 6,7 * * * and 0 18,19 29-31 12 *. ReminderPolicy.IsRunTime gates before any DB access.
Dockerfile                    One image: /app/api (default entrypoint, :8080) + /app/jobs; job overrides the command
src/web                       Vite + React 19 + TS 5.9 + React Router 7 + TanStack Query, PWA, openapi-fetch typed
                              client (npm run generate:api), hooks in src/hooks/queries.ts, strings in
                              src/i18n/sv.json via t(), screens in src/pages
tests/IskBattle.Domain.Tests  Pure unit tests
tests/IskBattle.Api.Tests     Testcontainers SQL Server (assembly fixture), WebApplicationFactory (ApiFactory with
                              TestAuthHandler, TestClock, RecordingEmailSender, FakePaymentGateway), persistence,
                              reminder job and Stripe signature tests
```

## Key decisions beyond the original docs (all recorded in GAME-DESIGN.md / ARCHITECTURE.md)

- Periods are clipped to the season (ISO week 2026-W53 splits at New Year). Dag = trading day → next trading day
  (weekends count toward Friday). "Starts the season" = no trading day in the season before the period start.
- Ny beats Pausad; Pausad shows last YTD of the same season only. Ties on 2-dp rounded returns, competition ranking
  1,1,3, ties/unranked sorted by name; Ranked → Ny → Pausad.
- Standings `date` only selects the period. Only Active members appear; join date ignored.
- Typo guard: >20 pp vs. most recent check-in before today in the same season.
- Earliest co-host = first to pay (`LeagueMember.PromotedAt`). Pending members may pay and become active co-hosts.
- Ownership on owner leave passes among already-active members before the queue is activated.
- A freed slot activates the first Pending member. Hosts can remove only ordinary members.
- Cadence change is queued via `League.NextCadenceSeason` (no rollover job). Extra endpoint
  `POST /leagues/{id}/invite-code`.
- Pending members see league name + upgrade options only (no member list/standings).
- One email switch (`EmailRemindersEnabled`) governs all email; unsubscribe turns it off. Reminders catch up within
  their window. Window claimed with a conditional update before sending (at most once).
- Stripe.net 53: `AllowedPaymentMethodTypes = [card, swish]`; webhook parsed with `throwOnApiVersionMismatch:false`.
  Concurrent same-tier buyer → `NeedsRefund`; overtaken by a smaller upgrade → `PaidNeedsPartialRefund`; both set
  `Purchase.RefundDueOre` (manual refund). Checkout returns 503 when Stripe keys are missing.
- Users keyed on Entra `oid`. Email/link options validated at startup (skipped for build-time OpenAPI generation).
- Check-in windows (GAME-DESIGN §4a, `CheckInWindows`): check-ins attributed to the trading day, open 22:30, close
  09:00 (Daily/Monthly next trading day) or Sunday midnight (Weekly); union of the player's cadences, Daily with no
  league; season end = window for all cadences closing at midnight 31 Dec. Reminders 08:00 the day after (20:00 on
  the season's last trading day). API tests' clock defaults to Monday 22:45 so the Daily window is open.
- Hosts get one pending-join email per player and league (`JoinPendingNotice`). `NameRules` rejects links/control chars in display and league
  names. Unsubscribe GET = confirm page, POST = change.

## Environment gotchas

- Run .NET tests with `DOCKER_API_VERSION=1.43 dotnet test` (local Docker Desktop is old, API 1.43). SQL Server image
  runs under amd64 emulation. CI on ubuntu doesn't need this.
- `dotnet test` uses Microsoft.Testing.Platform (configured in global.json); coverage via `coverlet.MTP` options
  (`--coverlet --coverlet-include ... --coverlet-threshold 100 --coverlet-threshold-type branch`).
- Repo `nuget.config` clears sources (the user's global config has an unreachable SkiStar feed).
- `TreatWarningsAsErrors` is on everywhere; xUnit1051 is suppressed only in Api.Tests.
- Generated enum types in `schema.d.ts` include `null`; `src/web/src/api/client.ts` exports NonNullable aliases.
- After API contract changes: build the API (regenerates openapi.json), then `npm run generate:api` in src/web.
  CI fails if either generated file is uncommitted.
- Local run: SQL container `isk-battle-sql` on 1433 (password in appsettings.Development.json), API on :5080 with
  `--launch-profile http`, Vite on :5173 proxying `/api`. Dev sign-in sends `X-Dev-User`. `Dev__Now=<ISO instant>`
  shifts the API/job clock in Development (`DevClock`, `AddDevClock`) to try windows at other times. Run the job
  locally: `DOTNET_ENVIRONMENT=Development dotnet run --project src/IskBattle.Jobs -- reminders`. No `chromium-cli` here: drive the
  UI with Playwright from a scratch folder (cached browser `~/Library/Caches/ms-playwright/chromium-1117`).

## Next steps

1. When the user has the accounts: milestone 8 – Bicep for SWA, Container Apps (min replicas 0), Azure SQL free
   offer (auto-pause at limit in dev, "continue using" before launch; Basic 5 DTU fallback), Container Apps Job
   (cron, same image), ACS Email + domain (SPF/DKIM/DMARC), Key Vault (Stripe keys, webhook secret,
   `Email:UnsubscribeKey`), App Insights, managed identity for SQL/Key Vault/ACS, deploy workflows, migrations in
   deploy, CORS for the app origin, custom domains. Also an Entra External ID SPA + API app registration
   (`VITE_ENTRA_*`, `Auth:Authority/Audience`); the API's access token **must include the `email` optional claim**
   (and `oid`), otherwise no email is ever sent. Docker Desktop must be running for the .NET tests.
2. Milestone 9 – Swedish terms and privacy policy (data stored: email, display name, YTD %, purchases; 7-year
   bookkeeping retention), company details in footer, Stripe live keys.
3. Open items worth raising with the user: Swedish exchange-holiday calendar.
