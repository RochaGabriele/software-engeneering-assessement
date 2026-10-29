# ADR-0001: Persist exchange rates as current state plus an append-only history

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Task 2 requires that "the latest rates are either updated and historically logged". The only reading that satisfies both verbs is: keep the latest rate per currency up to date **and** keep a log of every rate fetched. Task 3 then reads "the latest available exchange rate" on every catalog request.

Two technical constraints apply. The EF Core SQLite provider cannot translate `ORDER BY` or comparisons on `decimal` and `DateTimeOffset`, which makes "latest" queries over a log awkward. And the job may run more than once for the same provider publication: manual trigger, retries, the startup catch-up.

## Decision

Use two structures, written in one transaction through `IUnitOfWork`:

1. **`ExchangeRate` aggregate**, identified by `CurrencyPair` (USD→EUR, …), and its table `ExchangeRates` with one row per pair. `Update(rate, publishedAt, retrievedAt)` applies only when the provider's `publishedAt` is strictly newer than the stored one. Otherwise it reports *ignored*.
2. **`ExchangeRateHistoryEntry`**, an immutable append-only entity, and its table `ExchangeRateHistory`. An entry is appended only when an `ExchangeRate` is created or updated. A **unique index on (`BaseCurrency`, `QuoteCurrency`, `PublishedAt`)** guarantees idempotency at the storage level.

The history is not a collection inside the aggregate: it would grow without bound and be loaded on every read, and no invariant spans the history.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| **Append-only snapshots** (one snapshot per run; "latest" = most recent) | "Updated" is not satisfied literally. Every catalog read must sort by date, which SQLite cannot translate for `DateTimeOffset` without workarounds. A partial snapshot blurs "latest per currency" |
| **Temporal table / SCD type 2** (`ValidFrom`/`ValidTo` in one table) | One table, but every write closes one row and opens another. "Exactly one open row per pair" needs a filtered unique index. Harder to read and to explain for no gain here |
| **History inside the aggregate** | Unbounded aggregate and no invariant that needs it |

## Consequences

- ➕ Task 3 reads the current rate by primary key: one indexed lookup, no sorting.
- ➕ Re-running the job is harmless: no stale overwrite and no duplicate history.
- ➕ History is queryable later (audit, charts) without touching the read path.
- ➖ Two tables must stay consistent. This is handled by a single `SaveChanges` and covered by tests.
- ➖ The history stores the rate fields again. This is negligible at 4 currencies × 52 weeks.
