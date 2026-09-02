using GymTracker.Core.Domain;

namespace GymTracker.Application;

public sealed record BackupDocument(
    string FormatVersion,
    DateTimeOffset ExportedAt,
    string Checksum,
    IReadOnlyList<Exercise> Exercises,
    IReadOnlyList<ExerciseTemplate> ExerciseTemplates,
    IReadOnlyList<PlannedSession> PlannedSessions,
    IReadOnlyList<WorkoutSessionRecord> WorkoutSessions,
    IReadOnlyList<WorkoutSetRecord> WorkoutSets,
    IReadOnlyList<ActivityRecord> Activities,
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<UserSettings> UserSettings,
    ActiveWorkoutSnapshot? ActiveWorkout);

public enum BackupImportMode
{
    Replace,
    Merge
}

public sealed record BackupValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record BackupMutationResult(
    int InsertedRecords,
    int SkippedRecords,
    string? RecoveryCopyPath = null,
    IReadOnlyList<string>? Errors = null)
{
    public IReadOnlyList<string> ValidationErrors => Errors ?? [];
}
