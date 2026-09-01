using GymTracker.Domain;

namespace GymTracker.Infrastructure;

public interface ILocalDataStore : IAsyncDisposable
{
    int SchemaVersion { get; }
    Task<GymTrackerData> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(GymTrackerData data, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);
    Task ImportAsync(ReadOnlyMemory<byte> export, CancellationToken cancellationToken = default);
}
