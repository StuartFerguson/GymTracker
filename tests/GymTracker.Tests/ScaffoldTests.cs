using GymTracker.Application;
using Microsoft.Extensions.DependencyInjection;

namespace GymTracker.Tests;

public class ScaffoldTests
{
    [Fact]
    public void ApplicationServicesCanBeConstructedWithoutBackendServices()
    {
        var services = new ServiceCollection();

        services.AddGymTrackerApplication();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void FirstReleaseJourneyRoutesAreDeclared()
    {
        var expectedRoutes = new[]
        {
            "Dashboard",
            "WeeklyPlan",
            "StartWorkout",
            "ActiveWorkout",
            "WorkoutSummary",
            "ActivityLog",
            "History",
            "ExerciseProgress",
            "BackupSettings"
        };

        Assert.Equal(expectedRoutes, AppRoutes.All);
    }

    [Fact]
    public void WorkoutSessionRecordsSetsAndCalculatesSummaryTotals()
    {
        var session = new WorkoutSession("Upper Body");

        session.AddSet("Bench Press", 60, 10);
        session.AddSet("Bench Press", 65, 8);

        Assert.Equal(2, session.TotalSets);
        Assert.Equal(1120m, session.TotalVolume);
        Assert.Equal("Upper Body", session.Name);
    }
}
