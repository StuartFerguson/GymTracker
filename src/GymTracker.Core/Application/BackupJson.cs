using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GymTracker.Core.Domain;

namespace GymTracker.Application;

public sealed record BackupJsonResult(BackupDocument? Document, IReadOnlyList<string> Errors)
{
    public bool IsValid => Document is not null && Errors.Count == 0;
}

public static class BackupJson
{
    private const string CurrentFormatVersion = "1";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static BackupDocument CreateDocument(
        IReadOnlyList<Exercise> exercises,
        IReadOnlyList<ExerciseTemplate> exerciseTemplates,
        IReadOnlyList<PlannedSession> plannedSessions,
        IReadOnlyList<WorkoutSessionRecord> workoutSessions,
        IReadOnlyList<WorkoutSetRecord> workoutSets,
        IReadOnlyList<ActivityRecord> activities,
        IReadOnlyList<Recommendation> recommendations,
        IReadOnlyList<UserSettings> userSettings,
        ActiveWorkoutSnapshot? activeWorkout,
        DateTimeOffset? exportedAt = null)
    {
        var document = new BackupDocument(
            CurrentFormatVersion,
            exportedAt ?? DateTimeOffset.UtcNow,
            "",
            exercises,
            exerciseTemplates,
            plannedSessions,
            workoutSessions,
            workoutSets,
            activities,
            recommendations,
            userSettings,
            activeWorkout);

        return document with { Checksum = ComputeChecksum(document) };
    }

    public static string Serialize(BackupDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var canonical = Canonicalize(document);
        var withChecksum = canonical with { Checksum = ComputeChecksum(canonical) };
        return JsonSerializer.Serialize(withChecksum, Options);
    }

    public static BackupJsonResult DeserializeAndValidate(string json, bool allowExternalReferences = false)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BackupJsonResult(null, ["JSON content is required."]);
        }

        BackupDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<BackupDocument>(json, Options);
        }
        catch (JsonException exception)
        {
            return new BackupJsonResult(null, [$"JSON is invalid: {exception.Message}"]);
        }

        if (document is null)
        {
            return new BackupJsonResult(null, ["JSON must contain a backup document."]);
        }

        var errors = BackupValidation.Validate(document, allowExternalReferences).Errors.ToList();
        if (string.IsNullOrWhiteSpace(document.Checksum))
        {
            errors.Add("Checksum is required.");
        }
        else if (!IsValidChecksum(document.Checksum, ComputeChecksum(document)))
        {
            errors.Add("Checksum does not match the backup contents.");
        }

        return new BackupJsonResult(document, errors);
    }

    private static bool IsValidChecksum(string supplied, string expected) =>
        supplied.Length == expected.Length &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(expected));

    private static string ComputeChecksum(BackupDocument document)
    {
        var payload = JsonSerializer.Serialize(Canonicalize(document with { Checksum = "" }), Options);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static BackupDocument Canonicalize(BackupDocument document) => document with
    {
        Exercises = (document.Exercises ?? []).OrderBy(item => item.Id).ToArray(),
        ExerciseTemplates = (document.ExerciseTemplates ?? []).OrderBy(item => item.Id).Select(template => template with
        {
            Items = (template.Items ?? []).OrderBy(item => item.Position).ThenBy(item => item.ExerciseId).ToArray()
        }).ToArray(),
        PlannedSessions = (document.PlannedSessions ?? []).OrderBy(item => item.PlannedDate).ThenBy(item => item.Position).ThenBy(item => item.Id).ToArray(),
        WorkoutSessions = (document.WorkoutSessions ?? []).OrderBy(item => item.StartedAt).ThenBy(item => item.Id).ToArray(),
        WorkoutSets = (document.WorkoutSets ?? []).OrderBy(item => item.WorkoutSessionId).ThenBy(item => item.SetNumber).ThenBy(item => item.Id).ToArray(),
        Activities = (document.Activities ?? []).OrderBy(item => item.RecordedAt).ThenBy(item => item.Id).ToArray(),
        Recommendations = (document.Recommendations ?? []).OrderBy(item => item.CreatedAt).ThenBy(item => item.Id).ToArray(),
        UserSettings = (document.UserSettings ?? []).OrderBy(item => item.Id).ToArray()
    };

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
