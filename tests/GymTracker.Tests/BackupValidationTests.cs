using GymTracker.Application;
using GymTracker.Core.Domain;

namespace GymTracker.Tests;

public sealed class BackupValidationTests
{
    [Fact]
    public void Valid_document_has_no_validation_errors()
    {
        var result = BackupValidation.Validate(CreateValidDocument());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validator_accumulates_version_required_value_duplicate_and_reference_errors()
    {
        var exerciseId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var document = CreateValidDocument() with
        {
            FormatVersion = "2",
            Exercises = [new Exercise(exerciseId, " ", (ExerciseType)99, MeasurementUnit.Kilograms, true), new Exercise(exerciseId, "Duplicate", ExerciseType.Weight, MeasurementUnit.Kilograms, true)],
            ExerciseTemplates = [new ExerciseTemplate(templateId, " ", [
                new ExerciseTemplateItem(Guid.NewGuid(), "Missing", 1, -1, null, -2),
                new ExerciseTemplateItem(Guid.NewGuid(), "Missing again", 1, 1, null, null)], DateTimeOffset.UtcNow)],
            PlannedSessions = [new PlannedSession(Guid.NewGuid(), Guid.NewGuid(), "Missing template", new DateOnly(2026, 9, 1), 0)],
            WorkoutSessions = [new WorkoutSessionRecord(sessionId, null, "Session", DateTimeOffset.UtcNow, null, "kg")],
            WorkoutSets = [
                new WorkoutSetRecord(Guid.NewGuid(), sessionId, Guid.NewGuid(), "Missing exercise", 1, -1, -1, "kg", null),
                new WorkoutSetRecord(Guid.NewGuid(), sessionId, Guid.NewGuid(), "Missing exercise", 1, null, null, "kg", null)]
        };

        var result = BackupValidation.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("FormatVersion", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Exercises", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("ExerciseTemplates", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("PlannedSessions", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("WorkoutSets", StringComparison.Ordinal));
        Assert.True(result.Errors.Count >= 8);
    }

    [Fact]
    public void Validator_rejects_negative_activity_values_and_blank_required_fields()
    {
        var document = CreateValidDocument() with
        {
            Activities = [new ActivityRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, " ", -1, -2, null)],
            Recommendations = [new Recommendation(Guid.NewGuid(), Guid.NewGuid(), " ", " ", " ", DateTimeOffset.UtcNow, false)],
            UserSettings = [new UserSettings(Guid.NewGuid(), MeasurementUnit.Kilograms, " ", DateTimeOffset.UtcNow)]
        };

        var result = BackupValidation.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Activities", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Recommendations", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("UserSettings", StringComparison.Ordinal));
    }

    private static BackupDocument CreateValidDocument()
    {
        var exerciseId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var plannedSessionId = Guid.NewGuid();
        var workoutSessionId = Guid.NewGuid();

        return new BackupDocument(
            "1",
            DateTimeOffset.UtcNow,
            "",
            [new Exercise(exerciseId, "Bench Press", ExerciseType.Weight, MeasurementUnit.Kilograms, true)],
            [new ExerciseTemplate(templateId, "Push", [new ExerciseTemplateItem(exerciseId, "Bench Press", 1, 3, 8, 60)], DateTimeOffset.UtcNow)],
            [new PlannedSession(plannedSessionId, templateId, "Push", new DateOnly(2026, 9, 1), 0)],
            [new WorkoutSessionRecord(workoutSessionId, plannedSessionId, "Push", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), "kg")],
            [new WorkoutSetRecord(Guid.NewGuid(), workoutSessionId, exerciseId, "Bench Press", 1, 60, 8, "kg", null)],
            [new ActivityRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, "Walking", 1800, 5000, null)],
            [new Recommendation(Guid.NewGuid(), exerciseId, "Bench Press", "progress", "Add weight", DateTimeOffset.UtcNow, false)],
            [new UserSettings(Guid.NewGuid(), MeasurementUnit.Kilograms, "Europe/London", DateTimeOffset.UtcNow)],
            new ActiveWorkoutSnapshot("Draft", "Push", [new WorkoutSetSnapshot("Bench Press", 60, 8, null, WorkoutSetStatus.Completed, false)]));
    }
}
