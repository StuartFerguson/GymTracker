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
            "EditWorkoutSet",
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
    public void WorkoutSessionRecordsBodyweightSetsWithoutWeight()
    {
        var session = new WorkoutSession("Upper Body");

        session.AddSet("Pull Up", null, 8, "Strict reps");

        var set = Assert.Single(session.Sets);
        Assert.Null(set.Weight);
        Assert.Equal(8, set.Reps);
        Assert.Equal("Strict reps", set.Notes);
        Assert.Equal(0m, set.Volume);
    }

    [Fact]
    public void WorkoutSessionRecordsNonCompletedSetsAndDumbbellConvention()
    {
        var session = new WorkoutSession("Upper Body");

        session.AddSet("Dumbbell Bench Press", 22.5m, 6, status: WorkoutSetStatus.Failed, isPerDumbbell: true);
        session.AddSet("Dumbbell Bench Press", null, 0, status: WorkoutSetStatus.Skipped);

        Assert.Equal(WorkoutSetStatus.Failed, session.Sets[0].Status);
        Assert.True(session.Sets[0].IsPerDumbbell);
        Assert.Equal(WorkoutSetStatus.Skipped, session.Sets[1].Status);
    }

    [Fact]
    public void WorkoutSessionUpdatesAnExistingSet()
    {
        var session = new WorkoutSession("Upper Body");
        session.AddSet("Bench Press", 60, 10);

        session.UpdateSet(0, 62.5m, 8, "Slower tempo", WorkoutSetStatus.Incomplete);

        var set = Assert.Single(session.Sets);
        Assert.Equal(62.5m, set.Weight);
        Assert.Equal(8, set.Reps);
        Assert.Equal("Slower tempo", set.Notes);
        Assert.Equal(WorkoutSetStatus.Incomplete, set.Status);
    }

    [Fact]
    public void WorkoutSessionProvidesLatestPreviousValueForAnExercise()
    {
        var previous = new[]
        {
            new WorkoutSet("Bench Press", 60, 10),
            new WorkoutSet("Bench Press", 62.5m, 8)
        };
        var session = new WorkoutSession("Upper Body", previous);

        var startingSet = session.GetPreviousSet("Bench Press");

        Assert.NotNull(startingSet);
        Assert.Equal(62.5m, startingSet.Weight);
        Assert.Equal(8, startingSet.Reps);
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
