using Demo.Infrastructure.BackgroundJobs;

namespace Demo.IntegrationTests.BackgroundJobs;

public class CronScheduleTests
{
    private const string MondayAt0600 = "0 6 * * 1";

    [Fact]
    public void PreviousOccurrence_LaterInTheWeek_IsThisWeeksMonday()
    {
        var previous = CronSchedule.PreviousOccurrence(MondayAt0600, TimeZoneInfo.Utc, Utc(2026, 10, 27, 9, 0));

        Assert.Equal(Utc(2026, 10, 26, 6, 0), previous);
    }

    [Fact]
    public void PreviousOccurrence_ExactlyAtTheOccurrence_IsThatOccurrence()
    {
        Assert.Equal(Utc(2026, 10, 26, 6, 0), CronSchedule.PreviousOccurrence(MondayAt0600, TimeZoneInfo.Utc, Utc(2026, 10, 26, 6, 0)));
    }

    [Fact]
    public void PreviousOccurrence_MondayBeforeTheRunTime_IsLastWeeksMonday()
    {
        Assert.Equal(Utc(2026, 10, 19, 6, 0), CronSchedule.PreviousOccurrence(MondayAt0600, TimeZoneInfo.Utc, Utc(2026, 10, 26, 5, 59)));
    }

    [Fact]
    public void PreviousOccurrence_FollowsTheConfiguredTimeZoneAcrossDaylightSavingTime()
    {
        // Lisbon moves to UTC+1 on Sunday 2026-03-29, so Monday 06:00 local is 05:00 UTC that week (a 7d-1h gap).
        var lisbon = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

        var previous = CronSchedule.PreviousOccurrence(MondayAt0600, lisbon, Utc(2026, 3, 30, 5, 30));

        Assert.Equal(Utc(2026, 3, 30, 5, 0), previous);
    }

    [Fact]
    public void PreviousOccurrence_DailySchedule_IsTodaysRun()
    {
        Assert.Equal(Utc(2026, 10, 27, 6, 0), CronSchedule.PreviousOccurrence("0 6 * * *", TimeZoneInfo.Utc, Utc(2026, 10, 27, 9, 0)));
    }

    [Fact]
    public void PreviousOccurrence_SupportsTheSecondsFieldLikeHangfire()
    {
        Assert.Equal(Utc(2026, 10, 26, 6, 0, 30), CronSchedule.PreviousOccurrence("30 0 6 * * 1", TimeZoneInfo.Utc, Utc(2026, 10, 27, 9, 0)));
    }

    [Fact]
    public void PreviousOccurrence_ExpressionThatNeverFired_IsNull()
    {
        // 30 February never exists.
        Assert.Null(CronSchedule.PreviousOccurrence("0 6 30 2 *", TimeZoneInfo.Utc, Utc(2026, 10, 27, 9, 0)));
    }

    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute, int second = 0) =>
        new(year, month, day, hour, minute, second, TimeSpan.Zero);
}
