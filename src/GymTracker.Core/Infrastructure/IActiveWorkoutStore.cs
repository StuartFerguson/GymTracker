using GymTracker.Application;

namespace GymTracker.Core.Infrastructure;

public interface IActiveWorkoutStore
{
    Task SaveAsync(ActiveWorkoutSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<ActiveWorkoutSnapshot?> LoadAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
