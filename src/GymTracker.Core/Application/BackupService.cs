using System.Text;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Application;

public sealed record BackupExportResult(string Path, string FileName, long SizeBytes, string Checksum, BackupDocument Document);

public sealed record BackupFileValidationResult(BackupDocument? Document, IReadOnlyList<string> Errors)
{
    public bool IsValid => Document is not null && Errors.Count == 0;
}

public sealed record BackupImportResult(BackupMutationResult? Mutation, IReadOnlyList<string> Errors)
{
    public bool IsSuccessful => Mutation is not null && Errors.Count == 0;
}

public sealed class BackupService(IBackupDataStore dataStore, IActiveWorkoutStore activeWorkoutStore, string appDataDirectory)
{
    public async Task<BackupExportResult> ExportAsync(CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataDirectory);
        var data = await dataStore.ReadAsync(cancellationToken);
        var document = BackupJson.CreateDocument(data.Exercises, data.ExerciseTemplates, data.PlannedSessions, data.WorkoutSessions,
            data.WorkoutSets, data.Activities, data.Recommendations, data.UserSettings, await activeWorkoutStore.LoadAsync(cancellationToken));
        var json = BackupJson.Serialize(document);
        Directory.CreateDirectory(appDataDirectory);
        var fileName = $"gymtracker-backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(appDataDirectory, fileName);
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken);
        File.Move(temporaryPath, path, true);
        return new BackupExportResult(path, fileName, new FileInfo(path).Length, document.Checksum, document);
    }

    public async Task<BackupFileValidationResult> ValidateFileAsync(string path, CancellationToken cancellationToken = default, BackupImportMode mode = BackupImportMode.Replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var result = BackupJson.DeserializeAndValidate(json, mode == BackupImportMode.Merge);
        if (!result.IsValid || mode != BackupImportMode.Merge) return new BackupFileValidationResult(result.Document, result.Errors);
        var data = new BackupDataSet(result.Document!.Exercises, result.Document.ExerciseTemplates, result.Document.PlannedSessions, result.Document.WorkoutSessions,
            result.Document.WorkoutSets, result.Document.Activities, result.Document.Recommendations, result.Document.UserSettings);
        var referenceErrors = await dataStore.ValidateMergeReferencesAsync(data, cancellationToken);
        return new BackupFileValidationResult(result.Document, result.Errors.Concat(referenceErrors).ToArray());
    }

    public async Task<BackupImportResult> ImportAsync(string path, BackupImportMode mode, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateFileAsync(path, cancellationToken, mode);
        if (!validation.IsValid)
        {
            return new BackupImportResult(null, validation.Errors);
        }

        var document = validation.Document!;
        var data = new BackupDataSet(document.Exercises, document.ExerciseTemplates, document.PlannedSessions, document.WorkoutSessions,
            document.WorkoutSets, document.Activities, document.Recommendations, document.UserSettings);
        BackupMutationResult mutation;
        if (mode == BackupImportMode.Replace)
        {
            Directory.CreateDirectory(appDataDirectory);
            var recoveryPath = Path.Combine(appDataDirectory, $"gymtracker-recovery-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.db");
            mutation = await dataStore.ReplaceAsync(data, recoveryPath, cancellationToken);
            if (document.ActiveWorkout is null) await activeWorkoutStore.ClearAsync(cancellationToken);
            else await activeWorkoutStore.SaveAsync(document.ActiveWorkout, cancellationToken);
        }
        else if (mode == BackupImportMode.Merge)
        {
            mutation = await dataStore.MergeAsync(data, cancellationToken);
            if (document.ActiveWorkout is not null) await activeWorkoutStore.SaveAsync(document.ActiveWorkout, cancellationToken);
        }
        else
        {
            return new BackupImportResult(null, [$"Import mode '{mode}' is not supported."]);
        }

        return new BackupImportResult(mutation, []);
    }
}
