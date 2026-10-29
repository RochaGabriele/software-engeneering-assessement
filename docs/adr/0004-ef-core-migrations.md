# ADR-0004: EF Core Migrations replace `EnsureCreated`

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

The baseline creates the schema with `Database.EnsureCreated()` and seeds inline in `Program.cs`. `EnsureCreated` creates the schema only when the database does not exist. On an existing `demo.db` it does nothing, so the exchange-rate tables of Task 2 would never be created and the app would fail at runtime. Task 2 also adds a column to `Products` (`PriceCurrency`). Schema evolution is required, and `EnsureCreated` cannot provide it.

## Decision

- Use **EF Core Migrations**, starting with one `InitialCreate` migration that covers `Products`, `ExchangeRates` and `ExchangeRateHistory`. Migrations live in `Demo.Infrastructure/Persistence/Migrations`.
- Apply them at startup with `Database.MigrateAsync()`. This keeps the README promise that the database "will be automatically created and seeded on startup".
- Move seeding to EF Core's `UseSeeding` / `UseAsyncSeeding` (EF Core 9+). Both run after migrations and are idempotent: they insert the baseline SKUs and prices only when `Products` is empty.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| **Keep `EnsureCreated`** | Cannot evolve an existing database. This is the blocking issue |
| **Seed through `HasData`** | Seed values become part of the migration model. Awkward with value objects and complex properties, and every seed change requires a new migration |
| **Apply migrations only through a deployment step** | The right choice in production. Kept as a recommendation, because the starter app promises automatic creation on startup |

## Consequences

- ➕ The schema is versioned, reviewable and evolvable.
- ➖ A `demo.db` created by the baseline (`EnsureCreated`, no migration history) cannot be upgraded. Startup detects it before migrating and stops with a message naming the file and telling the developer to delete it (it is recreated and re-seeded); data is never deleted automatically. Fresh clones are unaffected because `demo.db` is git-ignored.
- ➖ Migrating on startup is unsafe with multiple instances. This is acceptable for a single-instance demo and recorded in Task 1.
