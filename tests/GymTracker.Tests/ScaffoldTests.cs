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

    [Fact]
    public void FeaturePageStatesProvideSharedLoadingEmptyAndErrorContracts()
    {
        Assert.Equal(FeaturePageStateKind.Loading, FeaturePageStates.Loading.Kind);
        Assert.Equal("Loading", FeaturePageStates.Loading.Title);

        var empty = FeaturePageStates.Empty("No workouts yet", "Start a workout to see it here.");
        Assert.Equal(FeaturePageStateKind.Empty, empty.Kind);
        Assert.Equal("No workouts yet", empty.Title);
        Assert.Equal("Start a workout to see it here.", empty.Message);

        var error = FeaturePageStates.Error("Unable to load workouts", "Try again in a moment.");
        Assert.Equal(FeaturePageStateKind.Error, error.Kind);
        Assert.Equal("Unable to load workouts", error.Title);
        Assert.Equal("Try again in a moment.", error.Message);
    }
}
