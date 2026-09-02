using GymTracker.Application;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task Export_writes_a_versioned_file_with_active_workout()
    {
        var directory = NewDirectory();
        try
        {
            var activePath = Path.Combine(directory, "active.json");
            var activeStore = new JsonActiveWorkoutStore(activePath);
            await activeStore.SaveAsync(new ActiveWorkoutSnapshot("Draft", null, []));
            var service = new BackupService(new FakeBackupDataStore(), activeStore, directory);

            var result = await service.ExportAsync();

            Assert.True(File.Exists(result.Path));
            Assert.Equal("1", result.Document.FormatVersion);
            Assert.NotNull(result.Document.ActiveWorkout);
            Assert.Equal(result.Document.Checksum, BackupJson.DeserializeAndValidate(await File.ReadAllTextAsync(result.Path)).Document!.Checksum);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Import_rejects_invalid_file_without_calling_the_store()
    {
        var directory = NewDirectory();
        try
        {
            var store = new FakeBackupDataStore();
            var service = new BackupService(store, new JsonActiveWorkoutStore(Path.Combine(directory, "active.json")), directory);
            var path = Path.Combine(directory, "invalid.json");
            await File.WriteAllTextAsync(path, "{not-json");

            var result = await service.ImportAsync(path, BackupImportMode.Replace);

            Assert.False(result.IsSuccessful);
            Assert.NotEmpty(result.Errors);
            Assert.Equal(0, store.ReplaceCalls);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gymtracker-service-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
    }

    private sealed class FakeBackupDataStore : IBackupDataStore
    {
        public int ReplaceCalls { get; private set; }

        public Task<BackupDataSet> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackupDataSet([], [], [], [], [], [], [], []));

        public Task<BackupMutationResult> ReplaceAsync(BackupDataSet data, string recoveryCopyPath, CancellationToken cancellationToken = default)
        {
            ReplaceCalls++;
            return Task.FromResult(new BackupMutationResult(0, 0, recoveryCopyPath));
        }

        public Task<BackupMutationResult> MergeAsync(BackupDataSet data, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BackupMutationResult(0, 0));

        public Task CreateRecoveryCopyAsync(string destinationPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
