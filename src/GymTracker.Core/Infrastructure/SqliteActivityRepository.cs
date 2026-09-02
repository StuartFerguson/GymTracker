using System.Globalization;
using GymTracker.Application;
using Microsoft.Data.Sqlite;

namespace GymTracker.Core.Infrastructure;

public sealed class SqliteActivityRepository(string connectionString) : IActivityRepository
{
    private const string CreateTableSql = """
        CREATE TABLE IF NOT EXISTS activities (
            id TEXT NOT NULL PRIMARY KEY,
            recorded_at_utc TEXT NOT NULL,
            activity_type TEXT NOT NULL,
            duration_seconds INTEGER,
            distance_metres REAL,
            steps INTEGER,
            notes TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_activities_recorded_at
            ON activities (recorded_at_utc);
        """;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = CreateTableSql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddAsync(ActivityEntry activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO activities
                (id, recorded_at_utc, activity_type, duration_seconds, distance_metres, steps, notes)
            VALUES
                ($id, $recordedAt, $activityType, $duration, $distance, $steps, $notes);
            """;
        command.Parameters.AddWithValue("$id", activity.Id.ToString("D"));
        command.Parameters.AddWithValue("$recordedAt", activity.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$activityType", activity.Type.ToString());
        command.Parameters.AddWithValue("$duration", (object?)activity.DurationSeconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$distance", (object?)activity.DistanceMetres ?? DBNull.Value);
        command.Parameters.AddWithValue("$steps", (object?)activity.Steps ?? DBNull.Value);
        command.Parameters.AddWithValue("$notes", (object?)activity.Notes ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActivityEntry>> ListAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ArgumentException("The end date must not precede the start date.", nameof(to));
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, recorded_at_utc, activity_type, duration_seconds, distance_metres, steps, notes
            FROM activities
            WHERE recorded_at_utc >= $from AND recorded_at_utc < $to
            ORDER BY recorded_at_utc, id;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to.AddDays(1)));

        var activities = new List<ActivityEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var type = Enum.Parse<ActivityType>(reader.GetString(2), ignoreCase: false);
            activities.Add(new ActivityEntry(
                Guid.Parse(reader.GetString(0)),
                DateOnly.FromDateTime(DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)),
                type,
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return activities;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
}
