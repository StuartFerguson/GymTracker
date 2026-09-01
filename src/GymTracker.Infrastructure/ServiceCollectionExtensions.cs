using Microsoft.Extensions.DependencyInjection;

namespace GymTracker.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGymTrackerPersistence(this IServiceCollection services, string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        return services.AddSingleton<ILocalDataStore>(_ => new SqliteDataStore(databasePath));
    }
}
