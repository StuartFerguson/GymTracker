using GymTracker.Core.Domain;

namespace GymTracker.Core.Infrastructure;

public sealed record WorkoutHistoryEntry(
    WorkoutSessionRecord Session,
    IReadOnlyList<WorkoutSetRecord> Sets,
    int PlannedSetCount = 0);

public interface IWorkoutHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        WorkoutSessionRecord session,
        IReadOnlyList<WorkoutSetRecord> sets,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkoutHistoryEntry>> ListAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
