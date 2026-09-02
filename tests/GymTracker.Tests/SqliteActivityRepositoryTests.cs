using GymTracker.Application;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class SqliteActivityRepositoryTests
{
    [Fact]
    public async Task Repository_round_trip_preserves_optional_activity_values()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteActivityRepository($"Data Source={path};Pooling=False");
            await repository.InitializeAsync();
            var activity = ActivityLogging.Create(new DateOnly(2026, 9, 1), ActivityType.Walking, 1800, 5000, 6000, "Morning walk");

            await repository.AddAsync(activity);

            var loaded = await repository.ListAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1));
            var result = Assert.Single(loaded);
            Assert.Equal(activity, result);
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task List_returns_only_activities_in_the_inclusive_date_range()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteActivityRepository($"Data Source={path};Pooling=False");
            await repository.InitializeAsync();
            await repository.AddAsync(ActivityLogging.Create(new DateOnly(2026, 8, 31), ActivityType.Walking));
            await repository.AddAsync(ActivityLogging.Create(new DateOnly(2026, 9, 1), ActivityType.Running));

            var loaded = await repository.ListAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7));

            var result = Assert.Single(loaded);
            Assert.Equal(ActivityType.Running, result.Type);
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task List_rejects_a_reversed_date_range()
    {
        var repository = new SqliteActivityRepository("Data Source=:memory:");

        await Assert.ThrowsAsync<ArgumentException>(() => repository.ListAsync(new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public async Task Initialize_disposes_the_connection_when_open_fails()
    {
        var repository = new SqliteActivityRepository($"Data Source={Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "activity.db")};Pooling=False");

        await Assert.ThrowsAnyAsync<Exception>(() => repository.InitializeAsync());
    }

    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.db");

    private static void DeletePath(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
    }
}
