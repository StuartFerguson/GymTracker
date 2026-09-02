using GymTracker.Core.Domain;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class SqliteWorkoutHistoryRepositoryTests
{
    [Fact]
    public async Task Repository_round_trip_preserves_session_and_set_values()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteWorkoutHistoryRepository($"Data Source={path};Pooling=False");
            var session = new WorkoutSessionRecord(
                Guid.NewGuid(), null, "Push", new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 9, 1, 19, 0, 0, TimeSpan.Zero), "kg");
            var set = new WorkoutSetRecord(Guid.NewGuid(), session.Id, Guid.NewGuid(), "Bench Press", 1, 60, 8, "kg", "Strong", "Completed");

            await repository.SaveAsync(session, [set]);

            var result = Assert.Single(await repository.ListAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)));
            Assert.Equal(session, result.Session);
            Assert.Equal(set, Assert.Single(result.Sets));
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task List_returns_sessions_only_in_the_inclusive_date_range_newest_first()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteWorkoutHistoryRepository($"Data Source={path};Pooling=False");
            var older = Session(new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero));
            var first = Session(new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.Zero));
            var newer = Session(new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero));
            await repository.SaveAsync(older, []);
            await repository.SaveAsync(newer, []);
            await repository.SaveAsync(first, []);

            var results = await repository.ListAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));

            Assert.Equal([newer.Id, first.Id], results.Select(item => item.Session.Id));
        }
        finally
        {
            DeletePath(path);
        }
    }

    private static WorkoutSessionRecord Session(DateTimeOffset startedAt) =>
        new(Guid.NewGuid(), null, "Workout", startedAt, startedAt.AddHours(1), "kg");

    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.db");

    private static void DeletePath(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
    }
}
