namespace GymTracker.Core.Infrastructure;

public sealed record DatabaseMigration(int Version, string Sql);

public interface IMigrationStore
{
    Task<IReadOnlySet<int>> GetAppliedVersionsAsync(CancellationToken cancellationToken = default);

    Task ApplyAsync(DatabaseMigration migration, CancellationToken cancellationToken = default);
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        IMigrationStore store,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        var appliedVersions = (await store.GetAppliedVersionsAsync(cancellationToken)).ToHashSet();
        foreach (var migration in DatabaseSchema.Migrations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!appliedVersions.Contains(migration.Version))
            {
                await store.ApplyAsync(migration, cancellationToken);
            }
        }
    }
}

public static class DatabaseSchema
{
    public const int CurrentVersion = 3;

    public static IReadOnlyList<DatabaseMigration> Migrations { get; } =
    [
        new DatabaseMigration(1, """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER NOT NULL PRIMARY KEY,
                applied_at_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS exercises (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                exercise_type INTEGER NOT NULL,
                default_unit INTEGER NOT NULL,
                is_active INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS exercise_templates (
                id TEXT NOT NULL PRIMARY KEY,
                name TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
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
            CREATE TABLE IF NOT EXISTS planned_sessions (
                id TEXT NOT NULL PRIMARY KEY,
                template_id TEXT NOT NULL,
                template_name_snapshot TEXT NOT NULL,
                planned_date TEXT NOT NULL,
                position INTEGER NOT NULL
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
                notes TEXT
            );
            CREATE TABLE IF NOT EXISTS activities (
                id TEXT NOT NULL PRIMARY KEY,
                recorded_at_utc TEXT NOT NULL,
                activity_type TEXT NOT NULL,
                duration_seconds INTEGER,
                distance_metres REAL,
                steps INTEGER,
                notes TEXT
            );
            CREATE TABLE IF NOT EXISTS recommendations (
                id TEXT NOT NULL PRIMARY KEY,
                exercise_id TEXT NOT NULL,
                exercise_name_snapshot TEXT NOT NULL,
                rule_key TEXT NOT NULL,
                message TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                is_dismissed INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS user_settings (
                id TEXT NOT NULL PRIMARY KEY,
                preferred_unit INTEGER NOT NULL,
                time_zone_id TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS backup_metadata (
                id TEXT NOT NULL PRIMARY KEY,
                file_name TEXT NOT NULL,
                created_at_utc TEXT NOT NULL,
                size_bytes INTEGER NOT NULL,
                schema_version TEXT NOT NULL,
                checksum TEXT NOT NULL
            );
            """),
        new DatabaseMigration(2, """
            CREATE INDEX IF NOT EXISTS ix_workout_sessions_started_at
                ON workout_sessions (started_at_utc);
            CREATE INDEX IF NOT EXISTS ix_workout_sets_session
                ON workout_sets (workout_session_id, set_number);
            """),
        new DatabaseMigration(3, """
            CREATE TABLE IF NOT EXISTS schema_metadata (
                key TEXT NOT NULL PRIMARY KEY,
                value TEXT NOT NULL
            );
            """)
    ];
}
