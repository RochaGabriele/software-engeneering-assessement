namespace Demo.Infrastructure.BackgroundJobs;

public sealed class ExchangeRateSyncOptions
{
    public const string SectionName = "ExchangeRates:Sync";

    public bool Enabled { get; init; } = true;

    /// <summary>Every Monday at 06:00, after the FX market reopens for the week (ADR-0005).</summary>
    public string Cron { get; init; } = "0 6 * * 1";

    public string TimeZone { get; init; } = "UTC";

    /// <summary>Enqueue one immediate run when no rate is stored, so a fresh deployment does not wait for Monday.</summary>
    public bool RunOnStartupWhenEmpty { get; init; } = true;
}
