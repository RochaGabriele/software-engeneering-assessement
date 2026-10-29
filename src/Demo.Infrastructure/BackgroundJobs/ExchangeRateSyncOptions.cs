namespace Demo.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateSyncOptions
{
    public const string SectionName = "ExchangeRates:Sync";

    public bool Enabled { get; init; } = true;

    /// <summary>Every Monday at 06:00, after the FX market reopens for the week (ADR-0005).</summary>
    public string Cron { get; init; } = "0 6 * * 1";

    public string TimeZone { get; init; } = "UTC";

    /// <summary>
    /// On startup, enqueue one immediate run when a supported currency has no rate or a stale one: a fresh deployment,
    /// a newly configured currency, or a scheduled run missed while the app was down (MemoryStorage keeps no history).
    /// </summary>
    public bool CatchUpOnStartup { get; init; } = true;

    /// <summary>Age after which a stored rate counts as missed. Defaults to one schedule period (a week).</summary>
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromDays(7);
}
