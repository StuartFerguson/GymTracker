using GymTracker.Application;

namespace GymTracker.Tests;

public sealed class WorkoutHistoryMappingTests
{
    [Fact]
    public void ToRecords_maps_session_and_sets_to_persisted_history_contracts()
    {
        var catalog = new BuiltInWorkoutCatalog();
        var session = new WorkoutSession("Push");
        session.AddSet("Barbell Bench Press", 60, 8, "Good set");
        session.AddSet("Barbell Bench Press", null, 0, status: WorkoutSetStatus.Skipped);
        var started = new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);
        var completed = started.AddMinutes(45);

        var records = WorkoutHistoryMapping.ToRecords(session, catalog.Exercises, started, completed);

        Assert.Equal("Push", records.Session.TemplateNameSnapshot);
        Assert.Equal(started, records.Session.StartedAt);
        Assert.Equal(completed, records.Session.CompletedAt);
        Assert.Equal(2, records.Sets.Count);
        Assert.Equal(catalog.Exercises.Single(exercise => exercise.Name == "Barbell Bench Press").Id, records.Sets[0].ExerciseId);
        Assert.Equal("Completed", records.Sets[0].Status);
        Assert.Equal("Skipped", records.Sets[1].Status);
        Assert.Equal([1, 2], records.Sets.Select(set => set.SetNumber));
    }
}
