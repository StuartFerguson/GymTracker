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
}
