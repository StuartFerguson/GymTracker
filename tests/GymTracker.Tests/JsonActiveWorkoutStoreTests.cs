using GymTracker.Application;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class JsonActiveWorkoutStoreTests
{
    [Fact]
    public async Task Store_round_trip_preserves_a_snapshot()
    {
        var path = NewPath();
        try
        {
            var store = new JsonActiveWorkoutStore(path);
            var snapshot = new ActiveWorkoutSnapshot("Pull", "Pull", [
                new WorkoutSetSnapshot("Pull Up", null, 0, "failed rep", WorkoutSetStatus.Failed, false)]);

            await store.SaveAsync(snapshot);

            var loaded = await store.LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(snapshot.SessionName, loaded!.SessionName);
            Assert.Equal(snapshot.TemplateName, loaded.TemplateName);
            Assert.Equal(snapshot.Sets, loaded.Sets);
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task Malformed_snapshot_is_removed_and_returns_no_state()
    {
        var path = NewPath();
        try
        {
            await File.WriteAllTextAsync(path, "{not-json");
            var store = new JsonActiveWorkoutStore(path);

            Assert.Null(await store.LoadAsync());
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task Failed_replacement_leaves_the_last_valid_snapshot()
    {
        var path = NewPath();
        var temporaryPath = path + ".tmp";
        try
        {
            var store = new JsonActiveWorkoutStore(path);
            var first = new ActiveWorkoutSnapshot("Push", "Push", []);
            var second = new ActiveWorkoutSnapshot("Legs", "Legs", []);
            await store.SaveAsync(first);
            Directory.CreateDirectory(temporaryPath);

            await Assert.ThrowsAnyAsync<UnauthorizedAccessException>(() => store.SaveAsync(second));

            var loaded = await store.LoadAsync();
            Assert.NotNull(loaded);
            Assert.Equal(first.SessionName, loaded!.SessionName);
            Assert.Equal(first.TemplateName, loaded.TemplateName);
            Assert.Equal(first.Sets, loaded.Sets);
        }
        finally
        {
            DeletePath(path);
            DeletePath(temporaryPath);
        }
    }

    [Fact]
    public async Task Clear_removes_the_recoverable_snapshot()
    {
        var path = NewPath();
        try
        {
            var store = new JsonActiveWorkoutStore(path);
            await store.SaveAsync(new ActiveWorkoutSnapshot("Push", "Push", []));

            await store.ClearAsync();

            Assert.Null(await store.LoadAsync());
            Assert.False(File.Exists(path));
        }
        finally
        {
            DeletePath(path);
        }
    }

    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.json");

    private static void DeletePath(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }
}
