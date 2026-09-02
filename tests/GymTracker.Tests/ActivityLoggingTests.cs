using GymTracker.Application;

namespace GymTracker.Tests;

public sealed class ActivityLoggingTests
{
    [Fact]
    public void Create_requires_a_date_and_supported_activity_type()
    {
        Assert.Throws<ArgumentException>(() => ActivityLogging.Create(null, ActivityType.Walking));
        Assert.Throws<ArgumentException>(() => ActivityLogging.Create(DateOnly.FromDateTime(DateTime.UtcNow), null));
    }

    [Fact]
    public void Create_rejects_negative_metrics()
    {
        var date = new DateOnly(2026, 9, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => ActivityLogging.Create(date, ActivityType.Running, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActivityLogging.Create(date, ActivityType.Running, distanceMetres: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActivityLogging.Create(date, ActivityType.Running, steps: -1));
    }

    [Fact]
    public void Create_rejects_an_undefined_activity_type()
    {
        Assert.Throws<ArgumentException>(() => ActivityLogging.Create(new DateOnly(2026, 9, 1), (ActivityType)99));
    }

    [Fact]
    public void Pace_is_calculated_for_walking_and_running()
    {
        var walking = ActivityLogging.Create(new DateOnly(2026, 9, 1), ActivityType.Walking, 1800, 5000);
        var swimming = ActivityLogging.Create(new DateOnly(2026, 9, 1), ActivityType.Swimming, 1800, 500);

        Assert.Equal(TimeSpan.FromMinutes(6), ActivityLogging.CalculatePace(walking));
        Assert.Null(ActivityLogging.CalculatePace(swimming));
    }

    [Fact]
    public void Pace_is_missing_when_duration_or_distance_is_not_positive()
    {
        var noDuration = ActivityLogging.Create(new DateOnly(2026, 9, 1), ActivityType.Running, 0, 5000);
        var noDistance = ActivityLogging.Create(new DateOnly(2026, 9, 1), ActivityType.Running, 1800, 0);

        Assert.Null(ActivityLogging.CalculatePace(noDuration));
        Assert.Null(ActivityLogging.CalculatePace(noDistance));
    }

    [Fact]
    public void Weekly_summary_rejects_null_activity_input()
    {
        Assert.Throws<ArgumentNullException>(() => ActivityLogging.GetWeeklySummary(null!, new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void Weekly_summary_totals_duration_distance_and_frequency()
    {
        var activities = new[]
        {
            ActivityLogging.Create(new DateOnly(2026, 8, 31), ActivityType.Walking, 1800, 5000, 6000),
            ActivityLogging.Create(new DateOnly(2026, 9, 2), ActivityType.Running, 1200, 3000),
            ActivityLogging.Create(new DateOnly(2026, 9, 8), ActivityType.Swimming, 900, 400)
        };

        var summary = ActivityLogging.GetWeeklySummary(activities, new DateOnly(2026, 8, 31));

        Assert.Equal(2, summary.ActivityCount);
        Assert.Equal(3000, summary.TotalDurationSeconds);
        Assert.Equal(8000, summary.TotalDistanceMetres);
        Assert.Equal(1, summary.CountFor(ActivityType.Walking));
        Assert.Equal(1, summary.CountFor(ActivityType.Running));
        Assert.Equal(0, summary.CountFor(ActivityType.Swimming));
    }
}
