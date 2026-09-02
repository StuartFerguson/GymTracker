namespace GymTracker.Core.Domain;

public enum ExerciseType
{
    Weight,
    Repetition,
    Duration,
    Distance
}

public enum MeasurementUnit
{
    Kilograms,
    Repetitions,
    Seconds,
    Metres
}

public enum ExerciseCategory
{
    Chest,
    Back,
    Shoulders,
    Arms,
    Legs,
    Core,
    Cardio
}

public sealed record Exercise(
    Guid Id,
    string Name,
    ExerciseType Type,
    MeasurementUnit DefaultUnit,
    bool IsActive)
{
    public ExerciseCategory Category { get; init; } = ExerciseCategory.Cardio;
}

public sealed record WeeklyPlanDay(DayOfWeek Day, string TemplateName);

public sealed record ExerciseTemplateItem(
    Guid ExerciseId,
    string ExerciseNameSnapshot,
    int Position,
    int TargetSets,
    int? TargetRepetitions,
    decimal? TargetWeightKg);

public sealed record ExerciseTemplate(
    Guid Id,
    string Name,
    IReadOnlyList<ExerciseTemplateItem> Items,
    DateTimeOffset UpdatedAt);

public sealed record PlannedSession(
    Guid Id,
    Guid TemplateId,
    string TemplateNameSnapshot,
    DateOnly PlannedDate,
    int Position);

public sealed record WorkoutSessionRecord(
    Guid Id,
    Guid? PlannedSessionId,
    string TemplateNameSnapshot,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string WeightUnit)
{
    public DateTimeOffset StartedAtUtc => StartedAt.ToUniversalTime();
}

public sealed record WorkoutSetRecord(
    Guid Id,
    Guid WorkoutSessionId,
    Guid ExerciseId,
    string ExerciseNameSnapshot,
    int SetNumber,
    decimal? WeightKg,
    int? Repetitions,
    string Unit,
    string? Notes,
    string Status = "Completed");

public sealed record ActivityRecord(
    Guid Id,
    DateTimeOffset RecordedAt,
    string ActivityType,
    int DurationSeconds,
    decimal? DistanceMetres,
    string? Notes);

public sealed record Recommendation(
    Guid Id,
    Guid ExerciseId,
    string ExerciseNameSnapshot,
    string RuleKey,
    string Message,
    DateTimeOffset CreatedAt,
    bool IsDismissed);

public sealed record UserSettings(
    Guid Id,
    MeasurementUnit PreferredUnit,
    string TimeZoneId,
    DateTimeOffset UpdatedAt);

public sealed record BackupMetadata(
    Guid Id,
    string FileName,
    DateTimeOffset CreatedAt,
    long SizeBytes,
    string SchemaVersion,
    string Checksum);
