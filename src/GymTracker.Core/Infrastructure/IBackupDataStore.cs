using GymTracker.Core.Domain;
using GymTracker.Application;

namespace GymTracker.Core.Infrastructure;

public sealed record BackupDataSet(
    IReadOnlyList<Exercise> Exercises,
    IReadOnlyList<ExerciseTemplate> ExerciseTemplates,
    IReadOnlyList<PlannedSession> PlannedSessions,
    IReadOnlyList<WorkoutSessionRecord> WorkoutSessions,
    IReadOnlyList<WorkoutSetRecord> WorkoutSets,
    IReadOnlyList<ActivityRecord> Activities,
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<UserSettings> UserSettings);

public interface IBackupDataStore
{
    Task<BackupDataSet> ReadAsync(CancellationToken cancellationToken = default);

    Task<BackupMutationResult> ReplaceAsync(BackupDataSet data, string recoveryCopyPath, CancellationToken cancellationToken = default);

    Task<BackupMutationResult> MergeAsync(BackupDataSet data, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ValidateMergeReferencesAsync(BackupDataSet data, CancellationToken cancellationToken = default);

    Task CreateRecoveryCopyAsync(string destinationPath, CancellationToken cancellationToken = default);
}
