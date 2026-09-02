using Microsoft.Extensions.DependencyInjection;

namespace GymTracker.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGymTrackerApplication(this IServiceCollection services)
    {
        services.AddSingleton<ProgressService>();
        return services;
    }
}
