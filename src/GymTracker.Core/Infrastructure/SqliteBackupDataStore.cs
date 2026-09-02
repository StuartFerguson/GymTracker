using System.Globalization;
using GymTracker.Application;
using GymTracker.Core.Domain;
using Microsoft.Data.Sqlite;

namespace GymTracker.Core.Infrastructure;

public sealed class SqliteBackupDataStore(string connectionString) : IBackupDataStore
{
    private const string CreateSchemaSql = """
        CREATE TABLE IF NOT EXISTS exercises (
            id TEXT NOT NULL PRIMARY KEY, name TEXT NOT NULL, exercise_type INTEGER NOT NULL,
            default_unit INTEGER NOT NULL, is_active INTEGER NOT NULL, category INTEGER NOT NULL DEFAULT 6);
        CREATE TABLE IF NOT EXISTS exercise_templates (
            id TEXT NOT NULL PRIMARY KEY, name TEXT NOT NULL, updated_at_utc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS exercise_template_items (
            template_id TEXT NOT NULL, exercise_id TEXT NOT NULL, exercise_name_snapshot TEXT NOT NULL,
            position INTEGER NOT NULL, target_sets INTEGER NOT NULL, target_repetitions INTEGER,
            target_weight_kg REAL, PRIMARY KEY (template_id, position));
        CREATE TABLE IF NOT EXISTS planned_sessions (
            id TEXT NOT NULL PRIMARY KEY, template_id TEXT NOT NULL, template_name_snapshot TEXT NOT NULL,
            planned_date TEXT NOT NULL, position INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS workout_sessions (
            id TEXT NOT NULL PRIMARY KEY, planned_session_id TEXT, template_name_snapshot TEXT NOT NULL,
            started_at_utc TEXT NOT NULL, completed_at_utc TEXT, weight_unit TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS workout_sets (
            id TEXT NOT NULL PRIMARY KEY, workout_session_id TEXT NOT NULL, exercise_id TEXT NOT NULL,
            exercise_name_snapshot TEXT NOT NULL, set_number INTEGER NOT NULL, weight_kg REAL,
            repetitions INTEGER, unit TEXT NOT NULL, notes TEXT, status TEXT NOT NULL DEFAULT 'Completed');
        CREATE TABLE IF NOT EXISTS activities (
            id TEXT NOT NULL PRIMARY KEY, recorded_at_utc TEXT NOT NULL, activity_type TEXT NOT NULL,
            duration_seconds INTEGER, distance_metres REAL, steps INTEGER, notes TEXT);
        CREATE TABLE IF NOT EXISTS recommendations (
            id TEXT NOT NULL PRIMARY KEY, exercise_id TEXT NOT NULL, exercise_name_snapshot TEXT NOT NULL,
            rule_key TEXT NOT NULL, message TEXT NOT NULL, created_at_utc TEXT NOT NULL, is_dismissed INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS user_settings (
            id TEXT NOT NULL PRIMARY KEY, preferred_unit INTEGER NOT NULL, time_zone_id TEXT NOT NULL,
            updated_at_utc TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS backup_metadata (
            id TEXT NOT NULL PRIMARY KEY, file_name TEXT NOT NULL, created_at_utc TEXT NOT NULL,
            size_bytes INTEGER NOT NULL, schema_version TEXT NOT NULL, checksum TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS ix_workout_sessions_started_at ON workout_sessions (started_at_utc);
        CREATE INDEX IF NOT EXISTS ix_workout_sets_session ON workout_sets (workout_session_id, set_number);
        CREATE INDEX IF NOT EXISTS ix_activities_recorded_at ON activities (recorded_at_utc);
        """;

    public async Task<BackupDataSet> ReadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        var exercises = await ReadExercisesAsync(connection, cancellationToken);
        var templates = await ReadTemplatesAsync(connection, cancellationToken);
        var plannedSessions = await ReadPlannedSessionsAsync(connection, cancellationToken);
        var workoutSessions = await ReadWorkoutSessionsAsync(connection, cancellationToken);
        var workoutSets = await ReadWorkoutSetsAsync(connection, cancellationToken);
        var activities = await ReadActivitiesAsync(connection, cancellationToken);
        var recommendations = await ReadRecommendationsAsync(connection, cancellationToken);
        var settings = await ReadSettingsAsync(connection, cancellationToken);

        return new BackupDataSet(exercises, templates, plannedSessions, workoutSessions, workoutSets, activities, recommendations, settings);
    }

    public async Task CreateRecoveryCopyAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await connection.CloseAsync();
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.Copy(GetDatabasePath(), destinationPath, true);
    }

    public async Task<BackupMutationResult> ReplaceAsync(BackupDataSet data, string recoveryCopyPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        await CreateRecoveryCopyAsync(recoveryCopyPath, cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await ExecuteAsync(connection, transaction, "DELETE FROM exercise_template_items; DELETE FROM workout_sets; DELETE FROM planned_sessions; DELETE FROM workout_sessions; DELETE FROM exercise_templates; DELETE FROM exercises; DELETE FROM activities; DELETE FROM recommendations; DELETE FROM user_settings;", cancellationToken);
            var inserted = await InsertAllAsync(connection, transaction, data, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new BackupMutationResult(inserted, 0, recoveryCopyPath);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<BackupMutationResult> MergeAsync(BackupDataSet data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        await using var connection = await OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var inserted = 0;
        var skipped = 0;
        try
        {
            foreach (var exercise in data.Exercises)
            {
                if (await ExistsAsync(connection, transaction, "exercises", exercise.Id, cancellationToken)) { skipped++; continue; }
                await InsertExerciseAsync(connection, transaction, exercise, cancellationToken); inserted++;
            }

            foreach (var template in data.ExerciseTemplates)
            {
                if (await ExistsAsync(connection, transaction, "exercise_templates", template.Id, cancellationToken)) { skipped++; }
                else { await InsertTemplateAsync(connection, transaction, template, cancellationToken); inserted++; }
                foreach (var item in template.Items)
                {
                    if (await ExistsTemplateItemAsync(connection, transaction, template.Id, item.Position, cancellationToken)) { skipped++; continue; }
                    await InsertTemplateItemAsync(connection, transaction, template.Id, item, cancellationToken); inserted++;
                }
            }

            foreach (var session in data.PlannedSessions)
            {
                if (await ExistsAsync(connection, transaction, "planned_sessions", session.Id, cancellationToken)) { skipped++; continue; }
                await InsertPlannedSessionAsync(connection, transaction, session, cancellationToken); inserted++;
            }
            foreach (var session in data.WorkoutSessions)
            {
                if (await ExistsAsync(connection, transaction, "workout_sessions", session.Id, cancellationToken)) { skipped++; continue; }
                await InsertWorkoutSessionAsync(connection, transaction, session, cancellationToken); inserted++;
            }
            foreach (var set in data.WorkoutSets)
            {
                if (await ExistsAsync(connection, transaction, "workout_sets", set.Id, cancellationToken)) { skipped++; continue; }
                await InsertWorkoutSetAsync(connection, transaction, set, cancellationToken); inserted++;
            }
            foreach (var activity in data.Activities)
            {
                if (await ExistsAsync(connection, transaction, "activities", activity.Id, cancellationToken)) { skipped++; continue; }
                await InsertActivityAsync(connection, transaction, activity, cancellationToken); inserted++;
            }
            foreach (var recommendation in data.Recommendations)
            {
                if (await ExistsAsync(connection, transaction, "recommendations", recommendation.Id, cancellationToken)) { skipped++; continue; }
                await InsertRecommendationAsync(connection, transaction, recommendation, cancellationToken); inserted++;
            }
            foreach (var settings in data.UserSettings)
            {
                if (await ExistsAsync(connection, transaction, "user_settings", settings.Id, cancellationToken)) { skipped++; continue; }
                await InsertSettingsAsync(connection, transaction, settings, cancellationToken); inserted++;
            }

            await transaction.CommitAsync(cancellationToken);
            return new BackupMutationResult(inserted, skipped);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<int> InsertAllAsync(SqliteConnection connection, SqliteTransaction transaction, BackupDataSet data, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var exercise in data.Exercises) { await InsertExerciseAsync(connection, transaction, exercise, cancellationToken); count++; }
        foreach (var template in data.ExerciseTemplates)
        {
            await InsertTemplateAsync(connection, transaction, template, cancellationToken); count++;
            foreach (var item in template.Items) { await InsertTemplateItemAsync(connection, transaction, template.Id, item, cancellationToken); count++; }
        }
        foreach (var item in data.PlannedSessions) { await InsertPlannedSessionAsync(connection, transaction, item, cancellationToken); count++; }
        foreach (var item in data.WorkoutSessions) { await InsertWorkoutSessionAsync(connection, transaction, item, cancellationToken); count++; }
        foreach (var item in data.WorkoutSets) { await InsertWorkoutSetAsync(connection, transaction, item, cancellationToken); count++; }
        foreach (var item in data.Activities) { await InsertActivityAsync(connection, transaction, item, cancellationToken); count++; }
        foreach (var item in data.Recommendations) { await InsertRecommendationAsync(connection, transaction, item, cancellationToken); count++; }
        foreach (var item in data.UserSettings) { await InsertSettingsAsync(connection, transaction, item, cancellationToken); count++; }
        return count;
    }

    private static async Task<IReadOnlyList<Exercise>> ReadExercisesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, exercise_type, default_unit, is_active, category FROM exercises ORDER BY id;";
        var result = new List<Exercise>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Exercise(ParseGuid(reader.GetString(0)), reader.GetString(1), (ExerciseType)reader.GetInt32(2),
                (MeasurementUnit)reader.GetInt32(3), reader.GetInt32(4) != 0) { Category = (ExerciseCategory)reader.GetInt32(5) });
        }
        return result;
    }

    private static async Task<IReadOnlyList<ExerciseTemplate>> ReadTemplatesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var itemsByTemplate = new Dictionary<Guid, List<ExerciseTemplateItem>>();
        await using (var itemCommand = connection.CreateCommand())
        {
            itemCommand.CommandText = "SELECT template_id, exercise_id, exercise_name_snapshot, position, target_sets, target_repetitions, target_weight_kg FROM exercise_template_items ORDER BY template_id, position;";
            await using var reader = await itemCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var templateId = ParseGuid(reader.GetString(0));
                if (!itemsByTemplate.TryGetValue(templateId, out var items)) itemsByTemplate[templateId] = items = [];
                items.Add(new ExerciseTemplateItem(ParseGuid(reader.GetString(1)), reader.GetString(2), reader.GetInt32(3), reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6)));
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, updated_at_utc FROM exercise_templates ORDER BY id;";
        var result = new List<ExerciseTemplate>();
        await using var templateReader = await command.ExecuteReaderAsync(cancellationToken);
        while (await templateReader.ReadAsync(cancellationToken))
        {
            var id = ParseGuid(templateReader.GetString(0));
            result.Add(new ExerciseTemplate(id, templateReader.GetString(1), itemsByTemplate.GetValueOrDefault(id, []), ParseTimestamp(templateReader.GetString(2))));
        }
        return result;
    }

    private static async Task<IReadOnlyList<PlannedSession>> ReadPlannedSessionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, template_id, template_name_snapshot, planned_date, position FROM planned_sessions ORDER BY planned_date, position, id;";
        var result = new List<PlannedSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new PlannedSession(ParseGuid(reader.GetString(0)), ParseGuid(reader.GetString(1)), reader.GetString(2), DateOnly.Parse(reader.GetString(3), CultureInfo.InvariantCulture), reader.GetInt32(4)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<WorkoutSessionRecord>> ReadWorkoutSessionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, planned_session_id, template_name_snapshot, started_at_utc, completed_at_utc, weight_unit FROM workout_sessions ORDER BY started_at_utc, id;";
        var result = new List<WorkoutSessionRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WorkoutSessionRecord(ParseGuid(reader.GetString(0)), reader.IsDBNull(1) ? null : ParseGuid(reader.GetString(1)), reader.GetString(2), ParseTimestamp(reader.GetString(3)),
                reader.IsDBNull(4) ? null : ParseTimestamp(reader.GetString(4)), reader.GetString(5)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<WorkoutSetRecord>> ReadWorkoutSetsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, workout_session_id, exercise_id, exercise_name_snapshot, set_number, weight_kg, repetitions, unit, notes, status FROM workout_sets ORDER BY workout_session_id, set_number, id;";
        var result = new List<WorkoutSetRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new WorkoutSetRecord(ParseGuid(reader.GetString(0)), ParseGuid(reader.GetString(1)), ParseGuid(reader.GetString(2)), reader.GetString(3), reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5), reader.IsDBNull(6) ? null : reader.GetInt32(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetString(9)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<ActivityRecord>> ReadActivitiesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, recorded_at_utc, activity_type, duration_seconds, distance_metres, notes FROM activities ORDER BY recorded_at_utc, id;";
        var result = new List<ActivityRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new ActivityRecord(ParseGuid(reader.GetString(0)), ParseTimestamp(reader.GetString(1)), reader.GetString(2), reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
        }
        return result;
    }

    private static async Task<IReadOnlyList<Recommendation>> ReadRecommendationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, exercise_id, exercise_name_snapshot, rule_key, message, created_at_utc, is_dismissed FROM recommendations ORDER BY created_at_utc, id;";
        var result = new List<Recommendation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Recommendation(ParseGuid(reader.GetString(0)), ParseGuid(reader.GetString(1)), reader.GetString(2), reader.GetString(3), reader.GetString(4), ParseTimestamp(reader.GetString(5)), reader.GetInt32(6) != 0));
        }
        return result;
    }

    private static async Task<IReadOnlyList<UserSettings>> ReadSettingsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, preferred_unit, time_zone_id, updated_at_utc FROM user_settings ORDER BY id;";
        var result = new List<UserSettings>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new UserSettings(ParseGuid(reader.GetString(0)), (MeasurementUnit)reader.GetInt32(1), reader.GetString(2), ParseTimestamp(reader.GetString(3))));
        }
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var connection = new SqliteConnection(connectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = CreateSchemaSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = "PRAGMA table_info(exercises);";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hasCategory = false;
        while (await reader.ReadAsync(cancellationToken)) hasCategory |= string.Equals(reader.GetString(1), "category", StringComparison.OrdinalIgnoreCase);
        if (!hasCategory)
        {
            await reader.DisposeAsync();
            command.CommandText = "ALTER TABLE exercises ADD COLUMN category INTEGER NOT NULL DEFAULT 6;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> ExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, Guid id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT EXISTS (SELECT 1 FROM {table} WHERE id = $id);";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 0;
    }

    private static async Task<bool> ExistsTemplateItemAsync(SqliteConnection connection, SqliteTransaction transaction, Guid templateId, int position, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM exercise_template_items WHERE template_id = $template AND position = $position);";
        command.Parameters.AddWithValue("$template", templateId.ToString("D"));
        command.Parameters.AddWithValue("$position", position);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 0;
    }

    private static async Task InsertExerciseAsync(SqliteConnection connection, SqliteTransaction transaction, Exercise item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO exercises (id, name, exercise_type, default_unit, is_active, category) VALUES ($id, $name, $type, $unit, $active, $category);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$name", item.Name), ("$type", (int)item.Type), ("$unit", (int)item.DefaultUnit), ("$active", item.IsActive ? 1 : 0), ("$category", (int)item.Category));

    private static async Task InsertTemplateAsync(SqliteConnection connection, SqliteTransaction transaction, ExerciseTemplate item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO exercise_templates (id, name, updated_at_utc) VALUES ($id, $name, $updated);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$name", item.Name), ("$updated", item.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

    private static async Task InsertTemplateItemAsync(SqliteConnection connection, SqliteTransaction transaction, Guid templateId, ExerciseTemplateItem item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO exercise_template_items (template_id, exercise_id, exercise_name_snapshot, position, target_sets, target_repetitions, target_weight_kg) VALUES ($template, $exercise, $name, $position, $sets, $reps, $weight);", cancellationToken,
            ("$template", templateId.ToString("D")), ("$exercise", item.ExerciseId.ToString("D")), ("$name", item.ExerciseNameSnapshot), ("$position", item.Position), ("$sets", item.TargetSets), ("$reps", item.TargetRepetitions), ("$weight", item.TargetWeightKg));

    private static async Task InsertPlannedSessionAsync(SqliteConnection connection, SqliteTransaction transaction, PlannedSession item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO planned_sessions (id, template_id, template_name_snapshot, planned_date, position) VALUES ($id, $template, $name, $date, $position);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$template", item.TemplateId.ToString("D")), ("$name", item.TemplateNameSnapshot), ("$date", item.PlannedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), ("$position", item.Position));

    private static async Task InsertWorkoutSessionAsync(SqliteConnection connection, SqliteTransaction transaction, WorkoutSessionRecord item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO workout_sessions (id, planned_session_id, template_name_snapshot, started_at_utc, completed_at_utc, weight_unit) VALUES ($id, $planned, $name, $started, $completed, $unit);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$planned", item.PlannedSessionId?.ToString("D")), ("$name", item.TemplateNameSnapshot), ("$started", item.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture)), ("$completed", item.CompletedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)), ("$unit", item.WeightUnit));

    private static async Task InsertWorkoutSetAsync(SqliteConnection connection, SqliteTransaction transaction, WorkoutSetRecord item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO workout_sets (id, workout_session_id, exercise_id, exercise_name_snapshot, set_number, weight_kg, repetitions, unit, notes, status) VALUES ($id, $session, $exercise, $name, $number, $weight, $reps, $unit, $notes, $status);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$session", item.WorkoutSessionId.ToString("D")), ("$exercise", item.ExerciseId.ToString("D")), ("$name", item.ExerciseNameSnapshot), ("$number", item.SetNumber), ("$weight", item.WeightKg), ("$reps", item.Repetitions), ("$unit", item.Unit), ("$notes", item.Notes), ("$status", item.Status));

    private static async Task InsertActivityAsync(SqliteConnection connection, SqliteTransaction transaction, ActivityRecord item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO activities (id, recorded_at_utc, activity_type, duration_seconds, distance_metres, notes) VALUES ($id, $recorded, $type, $duration, $distance, $notes);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$recorded", item.RecordedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)), ("$type", item.ActivityType), ("$duration", item.DurationSeconds), ("$distance", item.DistanceMetres), ("$notes", item.Notes));

    private static async Task InsertRecommendationAsync(SqliteConnection connection, SqliteTransaction transaction, Recommendation item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO recommendations (id, exercise_id, exercise_name_snapshot, rule_key, message, created_at_utc, is_dismissed) VALUES ($id, $exercise, $name, $rule, $message, $created, $dismissed);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$exercise", item.ExerciseId.ToString("D")), ("$name", item.ExerciseNameSnapshot), ("$rule", item.RuleKey), ("$message", item.Message), ("$created", item.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)), ("$dismissed", item.IsDismissed ? 1 : 0));

    private static async Task InsertSettingsAsync(SqliteConnection connection, SqliteTransaction transaction, UserSettings item, CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction, "INSERT INTO user_settings (id, preferred_unit, time_zone_id, updated_at_utc) VALUES ($id, $unit, $zone, $updated);", cancellationToken,
            ("$id", item.Id.ToString("D")), ("$unit", (int)item.PreferredUnit), ("$zone", item.TimeZoneId), ("$updated", item.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string GetDatabasePath() => new SqliteConnectionStringBuilder(connectionString).DataSource;

    private static Guid ParseGuid(string value) => Guid.Parse(value);
    private static DateTimeOffset ParseTimestamp(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
