using GymTracker.Application;
using GymTracker.Core.Infrastructure;
using Microsoft.Extensions.Logging;

namespace GymTracker;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddGymTrackerApplication();
        builder.Services.AddSingleton<IActiveWorkoutStore>(_ =>
            new JsonActiveWorkoutStore(Path.Combine(FileSystem.AppDataDirectory, "active-workout.json")));
        builder.Services.AddSingleton<ActiveWorkoutRecovery>();
        builder.Services.AddSingleton<IActivityRepository>(_ =>
            new SqliteActivityRepository($"Data Source={Path.Combine(FileSystem.AppDataDirectory, "gymtracker.db")}"));
        builder.Services.AddSingleton<IWorkoutHistoryRepository>(_ =>
            new SqliteWorkoutHistoryRepository($"Data Source={Path.Combine(FileSystem.AppDataDirectory, "gymtracker.db")}"));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
