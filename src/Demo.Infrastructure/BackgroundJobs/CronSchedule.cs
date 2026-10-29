using Cronos;

namespace Demo.Infrastructure.BackgroundJobs;

/// <summary>Evaluates a Hangfire cron expression with Cronos, the library Hangfire itself uses, so both agree on every occurrence.</summary>
internal static class CronSchedule
{
    // Widening windows find the latest occurrence cheaply for any schedule, from every minute to once a year.
    private static readonly TimeSpan[] LookBackWindows = [TimeSpan.FromDays(1), TimeSpan.FromDays(8), TimeSpan.FromDays(32), TimeSpan.FromDays(367)];

    /// <returns>The most recent occurrence at or before <paramref name="now"/>, or <c>null</c> if it never fired within a year.</returns>
    public static DateTimeOffset? PreviousOccurrence(string cronExpression, TimeZoneInfo timeZone, DateTimeOffset now)
    {
        // Like Hangfire: five fields is the standard format, six fields adds seconds.
        var format = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
            ? CronFormat.IncludeSeconds
            : CronFormat.Standard;
        var expression = CronExpression.Parse(cronExpression, format);

        foreach (var window in LookBackWindows)
        {
            var previous = expression
                .GetOccurrences(now - window, now, timeZone, fromInclusive: true, toInclusive: true)
                .Select(occurrence => (DateTimeOffset?)occurrence)
                .LastOrDefault();

            if (previous is not null)
            {
                return previous;
            }
        }

        return null;
    }
}
