using System.Globalization;
using GymTracker.Core.Domain;
using GymTracker.Core.Infrastructure;
using Microsoft.Data.Sqlite;

namespace GymTracker.Tests;

public sealed class SqliteBackupDataStoreTests
{
    [Fact]
    public async Task Read_returns_every_persisted_table_with_relationships_and_values()
    {
        var path = NewPath();
        try
        {
            var connectionString = $"Data Source={path};Pooling=False";
            var repository = new SqliteBackupDataStore(connectionString);
            var exerciseId = Guid.NewGuid();
            var templateId = Guid.NewGuid();
            var plannedId = Guid.NewGuid();
            var workoutId = Guid.NewGuid();
            var setId = Guid.NewGuid();
            var activityId = Guid.NewGuid();
            var recommendationId = Guid.NewGuid();
            var settingsId = Guid.NewGuid();
            var now = new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE exercises (id TEXT PRIMARY KEY, name TEXT NOT NULL, exercise_type INTEGER NOT NULL, default_unit INTEGER NOT NULL, is_active INTEGER NOT NULL, category INTEGER NOT NULL DEFAULT 6);
                    CREATE TABLE exercise_templates (id TEXT PRIMARY KEY, name TEXT NOT NULL, updated_at_utc TEXT NOT NULL);
                    CREATE TABLE exercise_template_items (template_id TEXT NOT NULL, exercise_id TEXT NOT NULL, exercise_name_snapshot TEXT NOT NULL, position INTEGER NOT NULL, target_sets INTEGER NOT NULL, target_repetitions INTEGER, target_weight_kg REAL, PRIMARY KEY (template_id, position));
                    CREATE TABLE planned_sessions (id TEXT PRIMARY KEY, template_id TEXT NOT NULL, template_name_snapshot TEXT NOT NULL, planned_date TEXT NOT NULL, position INTEGER NOT NULL);
                    CREATE TABLE workout_sessions (id TEXT PRIMARY KEY, planned_session_id TEXT, template_name_snapshot TEXT NOT NULL, started_at_utc TEXT NOT NULL, completed_at_utc TEXT, weight_unit TEXT NOT NULL);
                    CREATE TABLE workout_sets (id TEXT PRIMARY KEY, workout_session_id TEXT NOT NULL, exercise_id TEXT NOT NULL, exercise_name_snapshot TEXT NOT NULL, set_number INTEGER NOT NULL, weight_kg REAL, repetitions INTEGER, unit TEXT NOT NULL, notes TEXT, status TEXT NOT NULL);
                    CREATE TABLE activities (id TEXT PRIMARY KEY, recorded_at_utc TEXT NOT NULL, activity_type TEXT NOT NULL, duration_seconds INTEGER, distance_metres REAL, steps INTEGER, notes TEXT);
                    CREATE TABLE recommendations (id TEXT PRIMARY KEY, exercise_id TEXT NOT NULL, exercise_name_snapshot TEXT NOT NULL, rule_key TEXT NOT NULL, message TEXT NOT NULL, created_at_utc TEXT NOT NULL, is_dismissed INTEGER NOT NULL);
                    CREATE TABLE user_settings (id TEXT PRIMARY KEY, preferred_unit INTEGER NOT NULL, time_zone_id TEXT NOT NULL, updated_at_utc TEXT NOT NULL);
                    INSERT INTO exercises VALUES ($exercise, 'Bench Press', 0, 0, 1, 0);
                    INSERT INTO exercise_templates VALUES ($template, 'Push', $now);
                    INSERT INTO exercise_template_items VALUES ($template, $exercise, 'Bench Press', 1, 3, 8, 60);
                    INSERT INTO planned_sessions VALUES ($planned, $template, 'Push', '2026-09-02', 0);
                    INSERT INTO workout_sessions VALUES ($workout, $planned, 'Push', $now, $now, 'kg');
                    INSERT INTO workout_sets VALUES ($set, $workout, $exercise, 'Bench Press', 1, 60, 8, 'kg', 'Strong', 'Completed');
                    INSERT INTO activities VALUES ($activity, $now, 'Walking', 1800, 5000, NULL, 'Walk');
                    INSERT INTO recommendations VALUES ($recommendation, $exercise, 'Bench Press', 'progress', 'Add weight', $now, 0);
                    INSERT INTO user_settings VALUES ($settings, 0, 'Europe/London', $now);
                    """;
                command.Parameters.AddWithValue("$exercise", exerciseId.ToString("D"));
                command.Parameters.AddWithValue("$template", templateId.ToString("D"));
                command.Parameters.AddWithValue("$planned", plannedId.ToString("D"));
                command.Parameters.AddWithValue("$workout", workoutId.ToString("D"));
                command.Parameters.AddWithValue("$set", setId.ToString("D"));
                command.Parameters.AddWithValue("$activity", activityId.ToString("D"));
                command.Parameters.AddWithValue("$recommendation", recommendationId.ToString("D"));
                command.Parameters.AddWithValue("$settings", settingsId.ToString("D"));
                command.Parameters.AddWithValue("$now", now.ToString("O", CultureInfo.InvariantCulture));
                await command.ExecuteNonQueryAsync();
            }

            var result = await repository.ReadAsync();

            var exercise = Assert.Single(result.Exercises);
            Assert.Equal(exerciseId, exercise.Id);
            var template = Assert.Single(result.ExerciseTemplates);
            Assert.Equal(exerciseId, Assert.Single(template.Items).ExerciseId);
            Assert.Equal(plannedId, Assert.Single(result.PlannedSessions).Id);
            Assert.Equal(workoutId, Assert.Single(result.WorkoutSessions).Id);
            Assert.Equal("Strong", Assert.Single(result.WorkoutSets).Notes);
            Assert.Equal(activityId, Assert.Single(result.Activities).Id);
            Assert.Equal(recommendationId, Assert.Single(result.Recommendations).Id);
            Assert.Equal(settingsId, Assert.Single(result.UserSettings).Id);
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task Replace_creates_recovery_copy_and_replaces_all_rows()
    {
        var path = NewPath();
        var recoveryPath = path + ".recovery";
        try
        {
            var repository = new SqliteBackupDataStore($"Data Source={path};Pooling=False");
            await repository.ReadAsync();
            var existingId = Guid.NewGuid();
            await ExecuteAsync(path, $"INSERT INTO exercises (id, name, exercise_type, default_unit, is_active, category) VALUES ('{existingId:D}', 'Old', 0, 0, 1, 0);");
            var importedId = Guid.NewGuid();
            var data = new BackupDataSet(
                [new Exercise(importedId, "New", ExerciseType.Weight, MeasurementUnit.Kilograms, true) with { Category = ExerciseCategory.Chest }],
                [], [], [], [], [], [], []);

            var result = await repository.ReplaceAsync(data, recoveryPath);

            Assert.Equal(1, result.InsertedRecords);
            Assert.True(File.Exists(recoveryPath));
            var loaded = await repository.ReadAsync();
            Assert.Equal(importedId, Assert.Single(loaded.Exercises).Id);
            Assert.Contains("Old", await File.ReadAllTextAsync(recoveryPath));
        }
        finally
        {
            DeletePath(path);
            DeletePath(recoveryPath);
        }
    }

    [Fact]
    public async Task Merge_inserts_new_rows_and_reports_existing_id_conflicts()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteBackupDataStore($"Data Source={path};Pooling=False");
            await repository.ReadAsync();
            var existingId = Guid.NewGuid();
            await ExecuteAsync(path, $"INSERT INTO exercises (id, name, exercise_type, default_unit, is_active, category) VALUES ('{existingId:D}', 'Existing', 0, 0, 1, 0);");
            var newId = Guid.NewGuid();
            var result = await repository.MergeAsync(new BackupDataSet(
                [new Exercise(existingId, "Imported duplicate", ExerciseType.Weight, MeasurementUnit.Kilograms, true), new Exercise(newId, "Imported", ExerciseType.Distance, MeasurementUnit.Metres, true)],
                [], [], [], [], [], [], []));

            Assert.Equal(1, result.InsertedRecords);
            Assert.Equal(1, result.SkippedRecords);
            Assert.Equal(new[] { existingId, newId }.OrderBy(id => id), (await repository.ReadAsync()).Exercises.Select(item => item.Id).OrderBy(id => id));
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task Merge_reference_validation_accepts_references_to_existing_rows()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteBackupDataStore($"Data Source={path};Pooling=False");
            await repository.ReadAsync();
            var exerciseId = Guid.NewGuid();
            var templateId = Guid.NewGuid();
            await ExecuteAsync(path, $"INSERT INTO exercises (id, name, exercise_type, default_unit, is_active, category) VALUES ('{exerciseId:D}', 'Existing', 0, 0, 1, 0); INSERT INTO exercise_templates (id, name, updated_at_utc) VALUES ('{templateId:D}', 'Existing template', '2026-09-02T12:00:00.0000000+00:00');");

            var errors = await repository.ValidateMergeReferencesAsync(new BackupDataSet(
                [],
                [new ExerciseTemplate(Guid.NewGuid(), "Imported", [new ExerciseTemplateItem(exerciseId, "Existing", 0, 1, 1, null)], DateTimeOffset.UtcNow)],
                [new PlannedSession(Guid.NewGuid(), templateId, "Existing template", new DateOnly(2026, 9, 2), 0)],
                [], [], [], [], []));

            Assert.Empty(errors);
        }
        finally
        {
            DeletePath(path);
        }
    }

    [Fact]
    public async Task Merge_reference_validation_rejects_unresolved_references()
    {
        var path = NewPath();
        try
        {
            var repository = new SqliteBackupDataStore($"Data Source={path};Pooling=False");
            await repository.ReadAsync();
            var errors = await repository.ValidateMergeReferencesAsync(new BackupDataSet(
                [],
                [new ExerciseTemplate(Guid.NewGuid(), "Imported", [new ExerciseTemplateItem(Guid.NewGuid(), "Missing", 0, 1, 1, null)], DateTimeOffset.UtcNow)],
                [], [], [], [], [], []));

            Assert.NotEmpty(errors);
        }
        finally
        {
            DeletePath(path);
        }
    }

    private static string NewPath() => Path.Combine(Path.GetTempPath(), $"gymtracker-backup-{Guid.NewGuid():N}.db");

    private static async Task ExecuteAsync(string path, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void DeletePath(string path)
    {
        foreach (var candidate in new[] { path, path + "-shm", path + "-wal" })
        {
            if (File.Exists(candidate)) File.Delete(candidate);
        }
    }
}
