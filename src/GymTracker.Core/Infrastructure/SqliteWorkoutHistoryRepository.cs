using System.Globalization;
using GymTracker.Core.Domain;
using Microsoft.Data.Sqlite;

namespace GymTracker.Core.Infrastructure;

public sealed class SqliteWorkoutHistoryRepository(string connectionString) : IWorkoutHistoryRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS planned_sessions (
                id TEXT NOT NULL PRIMARY KEY,
                template_id TEXT NOT NULL,
                template_name_snapshot TEXT NOT NULL,
                planned_date TEXT NOT NULL,
                position INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS exercise_template_items (
                template_id TEXT NOT NULL,
                exercise_id TEXT NOT NULL,
                exercise_name_snapshot TEXT NOT NULL,
                position INTEGER NOT NULL,
                target_sets INTEGER NOT NULL,
                target_repetitions INTEGER,
                target_weight_kg REAL,
                PRIMARY KEY (template_id, position)
            );
            CREATE TABLE IF NOT EXISTS workout_sessions (
                id TEXT NOT NULL PRIMARY KEY,
                planned_session_id TEXT,
                template_name_snapshot TEXT NOT NULL,
                started_at_utc TEXT NOT NULL,
                completed_at_utc TEXT,
                weight_unit TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS workout_sets (
                id TEXT NOT NULL PRIMARY KEY,
                workout_session_id TEXT NOT NULL,
                exercise_id TEXT NOT NULL,
                exercise_name_snapshot TEXT NOT NULL,
                set_number INTEGER NOT NULL,
                weight_kg REAL,
                repetitions INTEGER,
                unit TEXT NOT NULL,
                notes TEXT,
                status TEXT NOT NULL DEFAULT 'Completed'
            );
            CREATE INDEX IF NOT EXISTS ix_workout_sessions_started_at
                ON workout_sessions (started_at_utc);
            CREATE INDEX IF NOT EXISTS ix_workout_sets_session
                ON workout_sets (workout_session_id, set_number);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        command.CommandText = "PRAGMA table_info(workout_sets);";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var hasStatus = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            hasStatus |= string.Equals(reader.GetString(1), "status", StringComparison.OrdinalIgnoreCase);
        }

        if (!hasStatus)
        {
            await reader.DisposeAsync();
            command.CommandText = "ALTER TABLE workout_sets ADD COLUMN status TEXT NOT NULL DEFAULT 'Completed';";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task SaveAsync(WorkoutSessionRecord session, IReadOnlyList<WorkoutSetRecord> sets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sets);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO workout_sessions (id, planned_session_id, template_name_snapshot, started_at_utc, completed_at_utc, weight_unit)
                VALUES ($id, $planned, $template, $started, $completed, $unit);
                """;
            command.Parameters.AddWithValue("$id", session.Id.ToString("D"));
            command.Parameters.AddWithValue("$planned", (object?)session.PlannedSessionId?.ToString("D") ?? DBNull.Value);
            command.Parameters.AddWithValue("$template", session.TemplateNameSnapshot);
            command.Parameters.AddWithValue("$started", session.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$completed", (object?)session.CompletedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
            command.Parameters.AddWithValue("$unit", session.WeightUnit);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var set in sets)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO workout_sets (id, workout_session_id, exercise_id, exercise_name_snapshot, set_number, weight_kg, repetitions, unit, notes, status)
                VALUES ($id, $session, $exercise, $name, $number, $weight, $repetitions, $unit, $notes, $status);
                """;
            command.Parameters.AddWithValue("$id", set.Id.ToString("D"));
            command.Parameters.AddWithValue("$session", set.WorkoutSessionId.ToString("D"));
            command.Parameters.AddWithValue("$exercise", set.ExerciseId.ToString("D"));
            command.Parameters.AddWithValue("$name", set.ExerciseNameSnapshot);
            command.Parameters.AddWithValue("$number", set.SetNumber);
            command.Parameters.AddWithValue("$weight", (object?)set.WeightKg ?? DBNull.Value);
            command.Parameters.AddWithValue("$repetitions", (object?)set.Repetitions ?? DBNull.Value);
            command.Parameters.AddWithValue("$unit", set.Unit);
            command.Parameters.AddWithValue("$notes", (object?)set.Notes ?? DBNull.Value);
            command.Parameters.AddWithValue("$status", set.Status);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkoutHistoryEntry>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (to < from) throw new ArgumentException("The end date must not precede the start date.", nameof(to));
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.id, s.planned_session_id, s.template_name_snapshot, s.started_at_utc, s.completed_at_utc, s.weight_unit,
                   COALESCE((SELECT SUM(i.target_sets) FROM planned_sessions p JOIN exercise_template_items i ON i.template_id = p.template_id WHERE p.id = s.planned_session_id), 0),
                   w.id, w.exercise_id, w.exercise_name_snapshot, w.set_number, w.weight_kg, w.repetitions, w.unit, w.notes, w.status
            FROM workout_sessions s LEFT JOIN workout_sets w ON w.workout_session_id = s.id
            WHERE s.started_at_utc >= $from AND s.started_at_utc < $to
            ORDER BY s.started_at_utc DESC, s.id DESC, w.set_number;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to.AddDays(1)));
        var results = new List<WorkoutHistoryEntry>();
        var plannedSetCount = 0;
        Guid? currentId = null;
        WorkoutSessionRecord? session = null;
        List<WorkoutSetRecord>? sets = null;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            if (currentId != id)
            {
                if (session is not null) results.Add(new WorkoutHistoryEntry(session, sets!, plannedSetCount));
                currentId = id;
                session = new WorkoutSessionRecord(id,
                    reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), reader.GetString(2),
                    ParseTimestamp(reader.GetString(3)), reader.IsDBNull(4) ? null : ParseTimestamp(reader.GetString(4)), reader.GetString(5));
                plannedSetCount = reader.GetInt32(6);
                sets = [];
            }

            if (!reader.IsDBNull(7))
            {
                sets!.Add(new WorkoutSetRecord(Guid.Parse(reader.GetString(7)), id, Guid.Parse(reader.GetString(8)), reader.GetString(9),
                    reader.GetInt32(10), reader.IsDBNull(11) ? null : reader.GetDecimal(11), reader.IsDBNull(12) ? null : reader.GetInt32(12),
                    reader.GetString(13), reader.IsDBNull(14) ? null : reader.GetString(14), reader.GetString(15)));
            }
        }
        if (session is not null) results.Add(new WorkoutHistoryEntry(session, sets!, plannedSetCount));
        return results;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var connection = new SqliteConnection(connectionString);
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static string FormatDate(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseTimestamp(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

}
