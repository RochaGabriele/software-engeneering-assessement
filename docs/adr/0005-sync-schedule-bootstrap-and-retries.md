# ADR-0005: Sync schedule, startup catch-up and retry layering

- **Status:** Accepted (revised 2026-10-29 after code review: the empty-database bootstrap became a general catch-up, and the job-level retry window was widened from minutes to hours)
- **Date:** 2026-10-26

## Context

Task 2 requires the sync to "run automatically every Monday" using Hangfire, which is configured with MemoryStorage. Four questions follow.

- **Monday when, and in which time zone?** FX markets close Friday ~22:00 UTC and reopen Sunday ~22:00 UTC, so rates fetched early on Monday UTC may still reflect Friday's close.
- **What happens right after a deployment, or after downtime?** With a strictly weekly job, a deployment on Tuesday leaves Task 3 without rates for six days. MemoryStorage also keeps no execution history, so Hangfire cannot tell that a Monday run was missed while the app was down; the rates would silently stay up to two weeks old.
- **What about a currency added to the configuration?** It would answer 503 until the next Monday.
- **How are failures retried** without multiplying attempts or burning the 1,000 requests/month quota?

## Decision

- **Schedule.** Recurring job `exchange-rates-sync`, cron `0 6 * * 1` with an explicit `UTC` time zone. Both come from configuration and are validated at startup. 06:00 UTC on Monday is after the Asian session opens, so the rates reflect the new week.
- **Registration.** `IRecurringJobManager.AddOrUpdate` runs on **every startup**, through a hosted scheduler. MemoryStorage forgets recurring jobs on restart, and `AddOrUpdate` is idempotent.
- **Startup catch-up.** If `CatchUpOnStartup` is set, the scheduler asks the application rule `IsSynchronizationDue`. It answers yes when a supported currency has no stored rate, or was last retrieved before the most recent occurrence of the cron schedule (computed with Cronos, the library Hangfire uses, in the configured time zone). A fixed age window (first revision) was replaced because an off-schedule run or a DST shift could hide a missed Monday. One run is then enqueued immediately. This single rule covers a fresh database, a newly configured currency and a Monday run missed during downtime. The weekly schedule is unchanged.
- **Concurrency.** `[DisableConcurrentExecution]` on the job, so a manual trigger, the catch-up and the schedule can never overlap.
- **Retry layering.**
  - HTTP level, seconds: `AddStandardResilienceHandler()` retries transient failures (5xx/408/429/network) within seconds, with attempt and total timeouts and a circuit breaker.
  - Job level, hours: `[AutomaticRetry(Attempts = 5)]` with explicit delays of 1 min, 5 min, 15 min, 1 h and 3 h rides out a provider outage of about 4 hours. Hangfire's default backoff would spend 3 attempts in about 4 minutes and lose the week.
  - Non-transient errors (401 invalid App ID, 403) are not retried at the HTTP level. They are still retried at the job level: distinguishing them would add complexity for a bounded cost (a handful of requests per week; none at all when the App ID is missing).
- **Thin job.** `ExchangeRatesSyncJob` only invokes the application handler and turns a failure result into an exception, so Hangfire records and retries it. Business logic stays out of the infrastructure.

## Alternatives considered

| Option | Why not chosen |
|---|---|
| `Cron.Weekly()` (Monday 00:00 UTC) | Sunday evening in the Americas, and thin market liquidity at that hour |
| No startup run | Catalog conversion unusable for up to a week after a fresh deployment |
| Startup run only on an empty database (first version) | Misses a Monday run lost to downtime and a newly configured currency |
| Register the job once (dashboard or one-off code) | Lost on every restart with MemoryStorage |
| Retries only in Hangfire | Every blip waits minutes and replays the whole job |
| Retries only over HTTP, or Hangfire's default job backoff | A provider outage longer than a few minutes would leave the week without rates |
| Persistent Hangfire storage instead of the catch-up rule | The right production choice (Hangfire then fires missed occurrences itself), recorded as recommendation F-15. The task states that Hangfire is already configured with MemoryStorage |

## Consequences

- ➕ Predictable, configurable schedule with an explicit time zone.
- ➕ Usable right after deployment; a missed Monday is recovered on the next startup; overlapping runs impossible.
- ➕ Worst-case quota use is bounded: (1 + 5 job attempts) × (1 + 3 HTTP retries) = 24 requests in a fully failing week, far below the monthly limit.
- ➖ The startup catch-up is an addition beyond the literal requirement. It is documented, switchable, and leaves the Monday schedule untouched.
- ➖ A downtime is caught up only when the app starts again; a process that stays up but misses a run (Hangfire server stopped) is not detected. Persistent storage would close that gap.
