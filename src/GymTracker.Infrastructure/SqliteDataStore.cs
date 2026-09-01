using System.Text.Json;
using GymTracker.Domain;
using Microsoft.Data.Sqlite;

namespace GymTracker.Infrastructure;

public sealed class SqliteDataStore(string databasePath) : ILocalDataStore
{
    private const int CurrentSchemaVersion = 1;
    private readonly string _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public int SchemaVersion { get; private set; }

    public async Task<GymTrackerData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT json FROM data_document WHERE id = 1";
        var json = await command.ExecuteScalarAsync(cancellationToken);
        if (json is not string document)
        {
            return new GymTrackerData { SchemaVersion = SchemaVersion };
        }

        return JsonSerializer.Deserialize<GymTrackerData>(document, _jsonOptions)
            ?? new GymTrackerData { SchemaVersion = SchemaVersion };
    }

    public async Task SaveAsync(GymTrackerData data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        await using var connection = await OpenAsync(cancellationToken);
        var persisted = data with { SchemaVersion = CurrentSchemaVersion };
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO data_document (id, json) VALUES (1, $json) ON CONFLICT(id) DO UPDATE SET json = excluded.json";
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(persisted, _jsonOptions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken);
        return JsonSerializer.SerializeToUtf8Bytes(data, _jsonOptions);
    }

    public async Task ImportAsync(ReadOnlyMemory<byte> export, CancellationToken cancellationToken = default)
    {
        var data = JsonSerializer.Deserialize<GymTrackerData>(export.Span, _jsonOptions)
            ?? throw new InvalidDataException("The export does not contain a GymTracker data document.");
        if (data.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidDataException($"The export schema version {data.SchemaVersion} is newer than supported version {CurrentSchemaVersion}.");
        }

        await SaveAsync(data, cancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(cancellationToken);
        await MigrateAsync(connection, cancellationToken);
        return connection;
    }

    private async Task MigrateAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS schema_info (id INTEGER PRIMARY KEY, version INTEGER NOT NULL);" +
                              "CREATE TABLE IF NOT EXISTS data_document (id INTEGER PRIMARY KEY, json TEXT NOT NULL);" +
                              "INSERT INTO schema_info (id, version) VALUES (1, 1) ON CONFLICT(id) DO NOTHING;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        SchemaVersion = CurrentSchemaVersion;
    }
}
