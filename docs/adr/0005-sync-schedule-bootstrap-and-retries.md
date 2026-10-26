# ADR-0005: Sync schedule, startup bootstrap and retry layering

- **Status:** Accepted
- **Date:** 2026-10-26

## Context

Task 2 requires the sync to "run automatically every Monday" using Hangfire, which is configured with MemoryStorage. Three questions follow.

- **Monday when, and in which time zone?** FX markets close Friday ~22:00 UTC and reopen Sunday ~22:00 UTC, so rates fetched early on Monday UTC may still reflect Friday's close.
- **What happens right after a deployment?** With a strictly weekly job, a deployment on Tuesday leaves Task 3 without rates for six days.
- **How are failures retried** without multiplying attempts or burning the 1,000 requests/month quota?

## Decision

- **Schedule.** Recurring job `exchange-rates-sync`, cron `0 6 * * 1` with an explicit `UTC` time zone. Both come from configuration and are validated at startup. 06:00 UTC on Monday is after the Asian session opens, so the rates reflect the new week.
- **Registration.** `IRecurringJobManager.AddOrUpdate` runs on **every startup**, through a hosted registrar. MemoryStorage forgets recurring jobs on restart, and `AddOrUpdate` is idempotent.
- **Bootstrap.** If `RunOnStartupWhenEmpty` is set and no rate is stored, one run is enqueued immediately. The weekly schedule is unchanged. The bootstrap only covers the empty-database gap and costs one request per fresh database.
- **Concurrency.** `[DisableConcurrentExecution]` on the job, so a manual trigger, the bootstrap and the schedule can never overlap.
- **Retry layering.**
  - HTTP level, short: `AddStandardResilienceHandler()` retries transient failures (5xx/408/429/network) within seconds, with attempt and total timeouts and a circuit breaker.
  - Job level, long: `[AutomaticRetry(Attempts = 3)]` reschedules the whole run with Hangfire's increasing delays for outages lasting minutes.
  - Non-transient errors (401 invalid App ID, 403) are not retried at the HTTP level.
- **Thin job.** `ExchangeRatesSyncJob` only invokes the application handler and turns a failure result into an exception, so Hangfire records and retries it. Business logic stays out of the infrastructure.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| `Cron.Weekly()` (Monday 00:00 UTC) | Sunday evening in the Americas, and thin market liquidity at that hour |
| No bootstrap | Catalog conversion unusable for up to a week after a fresh deployment |
| Register the job once (dashboard or one-off code) | Lost on every restart with MemoryStorage |
| Retries only in Hangfire | Every blip waits minutes and replays the whole job |
| Retries only over HTTP | A longer outage would leave the week without rates |

## Consequences

- ➕ Predictable, configurable schedule with an explicit time zone.
- ➕ Usable right after deployment; overlapping runs impossible.
- ➕ Worst-case quota use per run is bounded (HTTP attempts × job attempts), far below the monthly limit.
- ➖ The bootstrap is an addition beyond the literal requirement. It is documented, switchable, and leaves the Monday schedule untouched.
