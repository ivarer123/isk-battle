# ISK Battle — Game Design

> Working name. Source of truth for game rules. If code and this document disagree, this document wins until it is updated.
> UI language: Swedish (v1). Documentation language: English.

## 1. Concept

A season-long competition between friends (or communities) based on the development of their **main ISK account**. The goal is mutual motivation through a game — not verification, not trading, not prizes. The game runs on trust: players self-report, and cheating is accepted as part of the social contract.

## 2. Core loop

1. A player creates a **league** and shares an invite link.
2. Friends join via the link.
3. At the league's check-in cadence, each player enters **one number: their year-to-date (YTD) development % on their main ISK** ("Utveckling i år").
4. The app derives standings for every period from those YTD entries.
5. At the end of the calendar year, the player with the highest YTD is the **season champion**.

## 3. Season

- A season is a **calendar year** (1 January – 31 December).
- This aligns with the brokers' "i år" figure, which resets on 1 January.
- Season rollover requires no job: seasons are derived from check-in dates.
- Late joiners compete fairly: their YTD already covers the full year.

## 4. Check-ins

- A check-in is a single value: **YTD % on the player's main ISK**, e.g. `+12.40` or `-3.15`.
- Check-ins belong to the **player**, not the league. One check-in applies to every league the player is in.
- Stored against the player's local date (Europe/Stockholm). One check-in per player per date; re-submitting the same day overwrites.
- Only today's check-in may be edited. Past check-ins are immutable.
- Allowed range: `-99.99` to `+1000.00`.
- If the new value differs from the previous check-in by more than **20 percentage points**, the UI asks for confirmation (typo guard, not anti-cheat).
- Onboarding copy explains which account to use (Swedish, approximately): *"Välj ditt huvudsakliga ISK-konto och använd alltid samma. Ange utvecklingen i procent för i år, som du hittar på kontoöversikten hos din bank eller mäklare."*

## 5. Cadence

Each league picks its **most frequent** check-in cadence: `Daily`, `Weekly` or `Monthly`. All coarser standings are shown automatically.

| League cadence | Standings tabs shown |
|---|---|
| Daily | Dag, Vecka, Månad, År |
| Weekly | Vecka, Månad, År |
| Monthly | Månad, År |

- Cadence can only be changed **between seasons**. The UI may queue a change for the next season.
- Trading days in v1 = Monday–Friday. Swedish exchange holidays are a later refinement.

## 6. Standings

### Periods

| Period | Definition (Europe/Stockholm) |
|---|---|
| Dag | A trading day |
| Vecka | ISO week, Monday–Sunday |
| Månad | Calendar month |
| År | The season |

### Return for a period

For each member and period `[start, end)`:

- **latest** = the member's most recent check-in dated within the period.
- **base** = the member's most recent check-in dated before `start`, within the same season.
- If there is no base check-in:
  - If the period begins at the start of the season (no earlier trading day in the season), `base = 0`.
  - Otherwise the member is shown as **"Ny"** and is unranked for that period.
- If there is no latest check-in, the member is shown as **"Pausad"**, displays their last known YTD, and is unranked for that period.

```
periodReturn = ((1 + latest/100) / (1 + base/100) - 1) * 100
```

- **År** always uses `base = 0` and `latest` = most recent check-in in the season.
- Ranking: descending by period return. Equal returns share a rank.
- Unranked members ("Ny", "Pausad") are listed below ranked members.

### Clarifications

- **Periods never cross a season.** Every period is clipped to its calendar year. An ISO week spanning New Year (e.g. 2026-W53, 28 Dec – 3 Jan) is split into two parts, one per season.
- **Dag** runs from a trading day to the next trading day, so a weekend check-in counts toward Friday. The season's first Dag starts on 1 January even if that day is a weekend, and a weekend date resolves to the Dag that contains it.
- **"Begins at the start of the season"** means no trading day exists in the season before the period's start date.
- **Precedence:** a member with no base in a period that doesn't start the season is "Ny", even if they also have no latest. "Pausad" shows the last YTD from the same season (up to the period end), or nothing if there is none. Previous seasons are never shown.
- **Ranking** compares returns rounded to 2 decimals. Ranks use competition style (1, 1, 3). Ties, and unranked members within each group, are ordered by display name. Ranked members come first, then "Ny", then "Pausad".
- The requested date only selects the period. It is not an as-of cutoff.
- Only active members appear in standings. When a member joined the league doesn't matter.
- **Typo guard** compares with the most recent check-in dated before today in the same season. The first check-in of a season never asks for confirmation.

### No punishment for absence

There are no streaks and no penalties in v1. A player on holiday simply appears as "Pausad"; everyone else continues normally. Their YTD is carried forward and they rejoin the rankings at their next check-in.

## 7. Leagues

- A league has a name, a cadence, an invite code and a tier.
- A player can be in any number of leagues.
- Percentages and display names are visible **only to members of the same league**. No public profiles in v1.

### Roles

| Role | How you get it | Rights |
|---|---|---|
| Owner | Created the league | Everything, including deleting the league |
| Co-host | Paid for an upgrade of the league | Everything except deleting the league |
| Member | Joined via invite | Check in, view standings, leave |

Owner and co-hosts can: rename the league, queue a cadence change for next season, remove members, and share the invite link.

- If the owner leaves, ownership passes to the earliest co-host (the one who became co-host first, i.e. paid first); if there is none, to the longest-standing active member.
- If the last member leaves, the league is soft-deleted.

### Capacity and joining

- When a league is full, new joiners enter a **Pending** state instead of being rejected.
- The owner and co-hosts receive an email: *"{Namn} vill gå med i {Liga}. Uppgradera för att släppa in fler."*
- After an upgrade, pending members are activated **first-come, first-served** up to the new capacity.

## 8. Tiers and pricing

Premium is a **permanent, one-time** purchase attached to the **league**, not the buyer.

| Tier | Capacity | Price (SEK, incl. any moms) |
|---|---|---|
| Free | 10 | 0 |
| Premium 50 | 50 | 99 |
| Premium 100 | 100 | 199 |
| Premium 250 | 250 | 399 |

- Upgrades cost the **difference** between tiers (e.g. 50 → 100 costs 100 kr).
- Any league member may pay, including a Pending member. The payer becomes a co-host; a Pending payer is activated first.
- Prices are treated as moms-inclusive. The business is below the 120 000 kr moms threshold at launch; if it is crossed, prices stay the same and net revenue drops accordingly.
- Before checkout the buyer must tick: *"Jag vill att uppgraderingen aktiveras direkt och förstår att ångerrätten därmed upphör."* The consent timestamp is stored with the purchase.
- Deleting a paid league does not trigger a refund; the UI warns about this.

## 9. Notifications (email)

Reminders are per **player**, based on the most frequent cadence among their active leagues. At most one reminder per check-in window.

| Effective cadence | Reminder sent (Europe/Stockholm) | Condition |
|---|---|---|
| Daily | Mon–Fri 18:00 | No check-in today |
| Weekly | Friday 18:00 | No check-in since Monday |
| Monthly | Last weekday of the month 18:00 | No check-in this month |

Other emails: join pending (to owner and co-hosts), member activated (to the joiner). Every email has a one-click unsubscribe link. Players can turn reminders off in settings. Payment receipts are sent by Stripe.

## 10. Explicit non-goals (v1)

- No prizes, prize pools or entry fees — ever. Winners get the title only.
- No broker integrations or screenshots.
- No push notifications (email only).
- No streaks, badges or reactions.
- No public leaderboards or creator tools.
- No English UI.

## 11. Later ideas

Shareable weekly result cards, push notifications, badges and reactions, creator/community tier above 250, Swedish exchange holiday calendar, English UI.

## 12. Decision log

| Date | Decision |
|---|---|
| 2026-09-21 | Self-reported YTD % only; no broker APIs, no screenshots |
| 2026-09-21 | Season = calendar year; cadence Daily/Weekly/Monthly, most frequent wins |
| 2026-09-21 | One check-in per player applies to all leagues |
| 2026-09-21 | Free up to 10; permanent tiers 50/100/250 at 99/199/399 kr; upgrades pay the difference |
| 2026-09-21 | Owner = creator; payers become co-hosts |
| 2026-09-21 | Missed check-ins = "Pausad", no penalty |
| 2026-09-21 | Payments included in v1 |
| 2026-10-01 | §6 clarifications: periods clipped to season, Dag spans weekends, Ny before Pausad, 2-dp ties with competition ranking, typo guard within season |
| 2026-10-01 | Earliest co-host = first to pay; Pending members may pay and are activated as co-host |
