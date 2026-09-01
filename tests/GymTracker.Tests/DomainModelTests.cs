using GymTracker.Core.Domain;

namespace GymTracker.Tests;

public sealed class DomainModelTests
{
    [Fact]
    public void DomainModel_covers_the_persisted_concepts()
    {
        Assert.NotNull(typeof(Exercise));
        Assert.NotNull(typeof(ExerciseTemplate));
        Assert.NotNull(typeof(PlannedSession));
        Assert.NotNull(typeof(WorkoutSessionRecord));
        Assert.NotNull(typeof(WorkoutSetRecord));
        Assert.NotNull(typeof(ActivityRecord));
        Assert.NotNull(typeof(Recommendation));
        Assert.NotNull(typeof(UserSettings));
        Assert.NotNull(typeof(BackupMetadata));
    }

    [Fact]
    public void Historical_records_keep_the_values_needed_for_replay()
    {
        var startedAt = new DateTimeOffset(2026, 9, 1, 18, 30, 0, TimeSpan.Zero);
        var session = new WorkoutSessionRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Strength A",
            startedAt,
            null,
            "kg");
        var set = new WorkoutSetRecord(
            Guid.NewGuid(),
            session.Id,
            Guid.NewGuid(),
            "Back Squat",
            1,
            100m,
            5,
            "kg",
            "5 reps at 100 kg");

        Assert.Equal("Strength A", session.TemplateNameSnapshot);
        Assert.Equal("Back Squat", set.ExerciseNameSnapshot);
        Assert.Equal(100m, set.WeightKg);
        Assert.Equal(DateTimeKind.Utc, session.StartedAt.UtcDateTime.Kind);
    }

    [Fact]
    public void Measurement_units_are_explicit()
    {
        var exercise = new Exercise(
            Guid.NewGuid(),
            "Back Squat",
            ExerciseType.Weight,
            MeasurementUnit.Kilograms,
            true);

        Assert.Equal(MeasurementUnit.Kilograms, exercise.DefaultUnit);
        Assert.Equal(ExerciseType.Weight, exercise.Type);
    }
}
