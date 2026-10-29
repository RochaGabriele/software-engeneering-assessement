namespace Demo.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateSyncOptions
{
    public const string SectionName = "ExchangeRates:Sync";

    public bool Enabled { get; init; } = true;

    /// <summary>Every Monday at 06:00, after the FX market reopens for the week (ADR-0005).</summary>
    public string Cron { get; init; } = "0 6 * * 1";

    public string TimeZone { get; init; } = "UTC";

    /// <summary>
    /// On startup, enqueue one immediate run when a supported currency has no rate, or was last retrieved before the most
    /// recent scheduled occurrence: a fresh deployment, a newly configured currency, or a run missed while the app was
    /// down (MemoryStorage keeps no execution history, so Hangfire cannot detect that itself).
    /// </summary>
    public bool CatchUpOnStartup { get; init; } = true;
}
