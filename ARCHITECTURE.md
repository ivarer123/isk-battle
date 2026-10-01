# ISK Battle — Architecture

> Companion to `GAME-DESIGN.md`, which owns the rules. This document owns the technical design.
> Roles: this chat is the architect, Claude Code builds backend and frontend, Claude Design produces the UI.

## 1. Principles

- **Boring and cheap.** Traffic is tiny (one small write per player per check-in). Optimise for simplicity and near-zero idle cost, not scale.
- **Rules live in the Domain project** as pure, unit-tested C#. The API and Functions are thin shells around it.
- **Derived over stored.** Standings, seasons and period returns are computed from check-ins, never persisted.
- **Time is injected.** All time-dependent logic uses `TimeProvider` so rules are testable.
- **All business time is Europe/Stockholm.** Store UTC; convert at the domain boundary.

## 2. Stack

| Layer | Choice |
|---|---|
| Backend | .NET 10, ASP.NET Core minimal APIs |
| Data | Azure SQL Database (free offer), EF Core, migrations |
| Background | Azure Functions (.NET isolated, timer triggers) |
| Frontend | React + TypeScript + Vite, PWA, TanStack Query, React Router |
| Auth | Microsoft Entra External ID, email one-time passcode; MSAL React on the client, JWT bearer on the API |
| Email | Azure Communication Services Email, custom domain with SPF/DKIM/DMARC |
| Payments | Stripe Checkout (card + Swish), webhooks |
| Hosting | Azure Static Web Apps (frontend), Azure Container Apps consumption (API), Functions Flex Consumption |
| Images | GitHub Container Registry (avoids ACR cost) |
| Observability | Application Insights |
| IaC / CI | Bicep, GitHub Actions |

No SignalR: nothing is real-time.

## 3. Repository layout

```
isk-battle/
├── GAME-DESIGN.md
├── ARCHITECTURE.md
├── src/
│   ├── IskBattle.Domain/          # Entities, rules, scoring. No external deps.
│   ├── IskBattle.Infrastructure/  # EF Core, email, Stripe, time zone helpers
│   ├── IskBattle.Api/             # Minimal API endpoints, auth, webhooks
│   ├── IskBattle.Functions/       # Timer triggers (reminders)
│   └── web/                       # React + Vite PWA
├── tests/
│   ├── IskBattle.Domain.Tests/
│   └── IskBattle.Api.Tests/       # WebApplicationFactory + Testcontainers (SQL)
├── infra/                         # Bicep
└── .github/workflows/
```

Dependency direction: `Api → Infrastructure → Domain`, `Functions → Infrastructure → Domain`. Domain references nothing.

## 4. Domain model

```csharp
public enum Cadence { Daily, Weekly, Monthly }
public enum LeagueTier { Free, Premium50, Premium100, Premium250 }
public enum MemberRole { Owner, CoHost, Member }
public enum MemberStatus { Active, Pending }
public enum PurchaseStatus { Created, Paid, Failed, NeedsRefund }

public class User
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = "";      // Entra 'sub'
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool EmailRemindersEnabled { get; set; } = true;
    public string? LastReminderWindowKey { get; set; } // e.g. "D:2026-09-21", "W:2026-38", "M:2026-09"
    public DateTimeOffset CreatedAt { get; set; }
}

public class CheckIn
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly ForDate { get; set; }      // Europe/Stockholm date; unique (UserId, ForDate)
    public int Season => ForDate.Year;
    public decimal YtdPercent { get; set; }    // decimal(9,4), range -99.99..1000
    public DateTimeOffset RecordedAt { get; set; }
}

public class League
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public Cadence Cadence { get; set; }
    public Cadence? NextSeasonCadence { get; set; }
    public LeagueTier Tier { get; set; }
    public string InviteCode { get; set; } = "";   // 8 chars, unambiguous alphabet, unique
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public List<LeagueMember> Members { get; set; } = [];
}

public class LeagueMember
{
    public Guid LeagueId { get; set; }
    public Guid UserId { get; set; }
    public MemberRole Role { get; set; }
    public MemberStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }   // FIFO order for pending activation
}

public class Purchase
{
    public Guid Id { get; set; }
    public Guid LeagueId { get; set; }
    public Guid? BuyerUserId { get; set; }          // nulled on account deletion
    public LeagueTier FromTier { get; set; }
    public LeagueTier ToTier { get; set; }
    public int AmountOre { get; set; }              // money is always integer öre
    public string StripeCheckoutSessionId { get; set; } = "";  // unique
    public string? StripePaymentIntentId { get; set; }
    public PurchaseStatus Status { get; set; }
    public DateTimeOffset ImmediateDeliveryConsentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

### Tier configuration

All tunables live in one place, mirroring `GameConfig.cs` from Beerio Kart:

```csharp
public static class GameConfig
{
    public static readonly IReadOnlyDictionary<LeagueTier, (int Capacity, int PriceOre)> Tiers =
        new Dictionary<LeagueTier, (int, int)>
        {
            [LeagueTier.Free]       = (10, 0),
            [LeagueTier.Premium50]  = (50, 9_900),
            [LeagueTier.Premium100] = (100, 19_900),
            [LeagueTier.Premium250] = (250, 39_900),
        };

    public static int UpgradePriceOre(LeagueTier from, LeagueTier to) =>
        Tiers[to].PriceOre - Tiers[from].PriceOre;

    public const decimal MinYtdPercent = -99.99m;
    public const decimal MaxYtdPercent = 1000m;
    public const decimal ConfirmJumpThresholdPp = 20m;
    public static readonly TimeOnly ReminderTime = new(18, 0);
    public const string TimeZoneId = "Europe/Stockholm";
}
```

## 5. Domain services

| Service | Responsibility |
|---|---|
| `PeriodCalculator` | Given a Stockholm date and period type, returns `[start, end)`. Trading day = Mon–Fri in v1. ISO weeks. |
| `StandingsCalculator` | Pure function: members + their check-ins + period → ranked rows with status `Ranked`, `Ny`, `Pausad`. Implements §6 of `GAME-DESIGN.md` exactly. |
| `ReminderPolicy` | Given a user's effective cadence and "now", returns the current window key and whether a reminder is due. |
| `MembershipService` | Join (Active vs Pending), leave, ownership transfer, activation of pending members after upgrade. |
| `CheckInValidator` | Range checks, one-per-date, today-only edits, returns `RequiresConfirmation` on jumps above threshold. |

`StandingsCalculator` is the heart of the app. Requirements:

- 100% branch coverage in `IskBattle.Domain.Tests`.
- Test cases must include: first check-in of the season, mid-season joiner ("Ny"), absent player ("Pausad"), ties, negative YTD, January 1 boundary, year view with a single check-in.

Standings are computed on read. A league of 250 members with a season of daily check-ins is ~60 000 rows at worst — load only the check-ins needed (latest per member before `start`, latest per member within `[start, end)`) with two indexed queries.

## 6. API

All endpoints require a bearer token except the webhook and unsubscribe. On first authenticated request, `User` is upserted from the token claims.

| Method | Route | Notes |
|---|---|---|
| GET | `/me` | Profile + effective cadence |
| PATCH | `/me` | `displayName`, `emailRemindersEnabled` |
| DELETE | `/me` | GDPR delete: removes user, check-ins, memberships (with ownership transfer); nulls `Purchase.BuyerUserId` |
| POST | `/check-ins` | `{ ytdPercent, confirmed }` → 422 with `requiresConfirmation` if jump > threshold and not confirmed |
| GET | `/me/check-ins?season=2026` | Own history |
| POST | `/leagues` | `{ name, cadence }` → caller becomes Owner |
| GET | `/leagues` | Leagues the caller belongs to (Active or Pending) |
| GET | `/leagues/{id}` | Members only |
| PATCH | `/leagues/{id}` | Owner/co-host: `name`, `nextSeasonCadence` |
| DELETE | `/leagues/{id}` | Owner only; soft delete |
| POST | `/leagues/join` | `{ inviteCode }` → `Active` or `Pending`; notifies hosts if pending |
| DELETE | `/leagues/{id}/members/{userId}` | Host removes member, or self-leave |
| GET | `/leagues/{id}/standings?period=week&date=2026-09-21` | Members only |
| POST | `/leagues/{id}/checkout` | `{ targetTier, immediateDeliveryConsent: true }` → `{ checkoutUrl }` |
| POST | `/webhooks/stripe` | Signature-verified, idempotent |
| GET | `/unsubscribe?token=` | Signed token, no auth |

Conventions: ProblemDetails for errors, OpenAPI via `Microsoft.AspNetCore.OpenApi`, rate limiting on `/check-ins` and `/leagues/join`. Authorization rules are enforced in endpoint filters, not in the UI.

## 7. Payments

Flow:

1. Frontend shows upgrade options with prices from `GET /leagues/{id}` (server is the source of truth for prices).
2. User ticks the ångerrätt consent checkbox (required) and clicks pay.
3. `POST /leagues/{id}/checkout` creates a `Purchase` (`Created`) and a Stripe Checkout Session:
   - `mode: payment`, `currency: sek`, `payment_method_types: [card, swish]`
   - `line_items[0].price_data.unit_amount` = upgrade price in öre
   - `metadata: { purchaseId, leagueId, fromTier, toTier }`
   - `success_url` / `cancel_url` back to the league page
   - Stripe receipt emails enabled
4. Browser redirects to Stripe. Stripe hosts the payment page (no card data touches our servers).
5. Webhook handles `checkout.session.completed` (when `payment_status = paid`) and `checkout.session.async_payment_succeeded`:
   - Look up `Purchase` by session id. If already `Paid`, return 200 (idempotency).
   - In one transaction: set `Purchase` to `Paid`; set `League.Tier = max(current, ToTier)`; add or upgrade the buyer to `CoHost` (unless Owner); activate Pending members FIFO up to capacity.
   - If the league was already at or above `ToTier` (two concurrent buyers), mark `NeedsRefund` and log a warning. Refunds are handled manually in the Stripe dashboard in v1.
6. `checkout.session.async_payment_failed` / `expired` → `Failed`.

Local development: Stripe test mode + `stripe listen --forward-to localhost:5080/webhooks/stripe`.

## 8. Email and reminders

`IskBattle.Functions` hosts one timer trigger:

- `CheckInReminders`: runs every 15 minutes. For each user with reminders enabled and at least one active league: compute effective cadence (most frequent across active leagues), ask `ReminderPolicy` whether a reminder is due, skip if `LastReminderWindowKey` equals the current window key, send, then update the key. At most one reminder per window, even if the function runs twice.

Emails are sent through an `IEmailSender` abstraction:

- Production: Azure Communication Services Email.
- Development: logs rendered emails to the console.

Templates (Swedish, plain HTML + text): `CheckInReminder`, `JoinPending`, `MemberActivated`. Each contains a one-click unsubscribe link with an HMAC-signed token.

## 9. Frontend

- Vite + React + TS, installable PWA (manifest + service worker for the app shell only).
- **API contract:** generate a typed client from the API's OpenAPI document with `openapi-typescript`. UI components from Claude Design consume hooks, never `fetch` directly.
- Data access: one hook per resource (`useMe`, `useLeagues`, `useStandings`, `useCheckIn`) on TanStack Query.
- Auth: MSAL React; the token is attached by the API client.
- Strings: `src/web/src/i18n/sv.json`, accessed through a small `t()` helper so English can be added later.
- Numbers: format with `Intl.NumberFormat('sv-SE')` (comma decimal separator), and accept both `,` and `.` on input.

### Screens (for Claude Design)

1. Landing / sign-in
2. Onboarding: display name + "which account" explanation
3. Home: my leagues + prominent check-in card ("Uppdatera din utveckling")
4. Check-in dialog (with jump confirmation)
5. League view: period tabs, standings table (rank, name, return, YTD, status badge)
6. League settings (hosts): name, next-season cadence, members, invite link, upgrade
7. Upgrade: tier cards, consent checkbox, pay button
8. Join via invite link (Active or "Väntar på plats" state)
9. Profile: display name, reminders toggle, delete account
10. Legal: användarvillkor, integritetspolicy

## 10. Hosting and environments

| Resource | Tier |
|---|---|
| Static Web Apps | Free |
| Container Apps environment + app | Consumption, min replicas 0 |
| Azure SQL Database | Free offer (auto-pause) |
| Function App | Flex Consumption |
| Communication Services + Email | Pay per email |
| Key Vault | Standard (secrets: Stripe keys, webhook secret, HMAC key) |
| Application Insights | Pay-as-you-go (within free ingestion) |

- Custom domains: `app.<domain>` (SWA) and `api.<domain>` (Container Apps). Explicit CORS on the API allowing only the app origin.
- **Local development:** Vite dev server proxies `/api` to the local API, so there is no CORS locally and phones on the LAN work through the proxy (avoids the hub-URL/CORS issue from Beerio Kart). SQL Server in Docker, Stripe CLI for webhooks.
- The API uses managed identity for SQL and Key Vault.
- The frontend must tolerate a slow first request after SQL auto-pause (loading state, one retry).
- Environments: `local` and `prod` only in v1.

Expected cost: under ~100 kr/month up to several thousand active players.

## 11. Security and privacy

- Authorization checked server-side on every league endpoint (membership, role).
- Invite codes: 8 characters from an unambiguous alphabet; regenerable by hosts.
- Never store or display absolute SEK amounts of portfolios — only percentages.
- GDPR: account deletion endpoint; privacy policy listing data stored (email, display name, YTD percentages, purchases).
- Bookkeeping: purchase records are retained for 7 years (bokföringslagen), with the buyer reference removed on account deletion.
- Stripe webhook signature verification is mandatory; reject unsigned requests.

## 12. Build order (for Claude Code)

Each milestone ends with passing tests and a commit.

1. **Solution skeleton** — projects, references, CI build + test workflow.
2. **Domain** — entities, `GameConfig`, `PeriodCalculator`, `StandingsCalculator`, `CheckInValidator`, with full tests.
3. **Persistence** — EF Core model, migrations, indexes (`CheckIn(UserId, ForDate)` unique; `LeagueMember(LeagueId, Status, JoinedAt)`; `Purchase(StripeCheckoutSessionId)` unique).
4. **API core** — auth, `/me`, check-ins, leagues, join, standings. Integration tests.
5. **Frontend wiring** — generated client, hooks, routing, auth; Claude Design screens plugged in.
6. **Email + reminders** — `IEmailSender`, templates, Functions timer, `ReminderPolicy` tests.
7. **Payments** — checkout, webhook, pending activation, co-host promotion. Tested against Stripe test mode.
8. **Infrastructure** — Bicep, deploy workflows, custom domains, email domain verification.
9. **Legal pages + launch checklist** — terms, privacy policy, company details in footer, switch Stripe to live.

## 13. Open technical questions

- Swedish exchange holiday calendar (v1 treats all weekdays as trading days).
- Whether to move from the SQL free offer to paid serverless once paying users arrive (removes auto-pause latency).
- Domain name.
