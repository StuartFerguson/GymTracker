using GymTracker.Core.Infrastructure;

namespace GymTracker.Application;

public sealed record RecoveredWorkout(WorkoutSession Session, string? TemplateName);

public sealed class ActiveWorkoutRecovery(IActiveWorkoutStore store)
{
    public async Task SaveAsync(
        WorkoutSession session,
        string? templateName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await store.SaveAsync(session.ToSnapshot(templateName), cancellationToken);
    }

    public async Task<RecoveredWorkout?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await store.LoadAsync(cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        try
        {
            return new RecoveredWorkout(WorkoutSession.FromSnapshot(snapshot), snapshot.TemplateName);
        }
        catch (ArgumentException)
        {
            await store.ClearAsync(cancellationToken);
            return null;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        store.ClearAsync(cancellationToken);
}
