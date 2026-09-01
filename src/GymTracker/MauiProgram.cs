using GymTracker.Application;
using GymTracker.Infrastructure;
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
        builder.Services.AddGymTrackerPersistence(Path.Combine(FileSystem.AppDataDirectory, "gymtracker.db"));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
