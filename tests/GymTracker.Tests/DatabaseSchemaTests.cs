using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class DatabaseSchemaTests
{
    [Fact]
    public async Task Initialize_applies_pending_migrations_in_version_order()
    {
        var store = new FakeMigrationStore(1);

        await DatabaseInitializer.InitializeAsync(store);

        Assert.Equal(new[] { 2, 3 }, store.AppliedVersions);
        Assert.Equal(DatabaseSchema.CurrentVersion, store.AppliedVersions[^1]);
    }

    [Fact]
    public async Task Initialize_is_idempotent_when_schema_is_current()
    {
        var store = new FakeMigrationStore(1);

        await DatabaseInitializer.InitializeAsync(store);
        store.AppliedVersions.Clear();
        await DatabaseInitializer.InitializeAsync(store);

        Assert.Empty(store.AppliedVersions);
    }

    [Fact]
    public void Current_schema_contains_required_tables_and_storage_conventions()
    {
        var migration = DatabaseSchema.Migrations[0];

        Assert.Equal(1, migration.Version);
        Assert.Contains("CREATE TABLE IF NOT EXISTS exercises", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE IF NOT EXISTS workout_sessions", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("started_at_utc TEXT NOT NULL", migration.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("weight_kg REAL", migration.Sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeMigrationStore(params int[] appliedVersions) : IMigrationStore
    {
        private readonly HashSet<int> applied = appliedVersions.ToHashSet();

        public List<int> AppliedVersions { get; } = [];

        public Task<IReadOnlySet<int>> GetAppliedVersionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<int>>(applied);

        public Task ApplyAsync(DatabaseMigration migration, CancellationToken cancellationToken = default)
        {
            applied.Add(migration.Version);
            AppliedVersions.Add(migration.Version);
            return Task.CompletedTask;
        }
    }
}
