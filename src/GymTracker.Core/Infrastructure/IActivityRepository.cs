using GymTracker.Application;

namespace GymTracker.Core.Infrastructure;

public interface IActivityRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ActivityEntry activity, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActivityEntry>> ListAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
