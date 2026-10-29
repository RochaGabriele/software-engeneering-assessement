namespace Demo.Application.Modules.ExchangeRates.IsSynchronizationDue;

/// <param name="LastScheduledRunAt">
/// The most recent occurrence of the sync schedule, or <c>null</c> when it has not fired yet. The schedule (cron, time zone)
/// belongs to the infrastructure; this context only needs to know when a run should last have happened.
/// </param>
public sealed record IsSynchronizationDueQuery(DateTimeOffset? LastScheduledRunAt);
