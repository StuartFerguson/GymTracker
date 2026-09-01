using GymTracker.Domain;
using GymTracker.Infrastructure;
using System.Text.Json;

namespace GymTracker.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task StoreRoundTripPreservesHistoricalSnapshotsAndAllCollections()
    {
        var path = CreateDatabasePath();
        var expected = new GymTrackerData
        {
            Workouts =
            [
                new WorkoutRecord
                {
                    TemplateNameSnapshot = "Strength A",
                    Exercises = [new WorkoutExerciseRecord { ExerciseNameSnapshot = "Squat", Weight = 100, Repetitions = 5 }]
                }
            ],
            Templates = [new WorkoutTemplateRecord { Name = "Strength A" }],
            Exercises = [new ExerciseCatalogueEntry { Name = "Squat", MuscleGroup = "Legs" }],
            Activities = [new ActivityRecord { Name = "Run", DurationMinutes = 30 }],
            Recommendations = [new RecommendationRecord { Text = "Add 2.5kg" }],
            Settings = [new SettingRecord { Key = "units", Value = "metric" }],
            BackupMetadata = [new BackupMetadataRecord { FileName = "backup.json", CreatedAt = DateTimeOffset.UtcNow }]
        };

        await using (var store = new SqliteDataStore(path))
        {
            await store.SaveAsync(expected);
        }

        await using var reopened = new SqliteDataStore(path);
        var actual = await reopened.LoadAsync();

        Assert.Equal(JsonSerializer.Serialize(expected with { SchemaVersion = 1 }), JsonSerializer.Serialize(actual));
        Assert.Equal(1, reopened.SchemaVersion);
    }

    [Fact]
    public async Task ExportImportRestoresDataWithoutLosingFidelity()
    {
        var sourcePath = CreateDatabasePath();
        var targetPath = CreateDatabasePath();
        var expected = new GymTrackerData
        {
            Workouts = [new WorkoutRecord { TemplateNameSnapshot = "Template v1", Notes = "Original notes" }],
            Settings = [new SettingRecord { Key = "theme", Value = "dark" }]
        };

        await using (var source = new SqliteDataStore(sourcePath))
        {
            await source.SaveAsync(expected);
            var export = await source.ExportAsync();

            await using var target = new SqliteDataStore(targetPath);
            await target.ImportAsync(export);
            Assert.Equal(JsonSerializer.Serialize(expected with { SchemaVersion = 1 }), JsonSerializer.Serialize(await target.LoadAsync()));
        }
    }

    private static string CreateDatabasePath() => Path.Combine(Path.GetTempPath(), $"gym-tracker-{Guid.NewGuid():N}.db");
}
