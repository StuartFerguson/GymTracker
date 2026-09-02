using GymTracker.Application;
using GymTracker.Core.Domain;

namespace GymTracker.Tests;

public sealed class BackupJsonTests
{
    [Fact]
    public void Serialize_writes_camel_case_enum_strings_and_checksum()
    {
        var document = CreateDocument();

        var json = BackupJson.Serialize(document);

        Assert.Contains("formatVersion", json, StringComparison.Ordinal);
        Assert.Contains("weight", json, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatVersion", json, StringComparison.Ordinal);
        Assert.NotEmpty(document.Checksum);
    }

    [Fact]
    public void Serialize_orders_collections_deterministically_and_round_trips()
    {
        var first = CreateDocument(reverseExercises: false);
        var second = CreateDocument(reverseExercises: true) with { ExportedAt = first.ExportedAt };

        var firstJson = BackupJson.Serialize(first);
        var secondJson = BackupJson.Serialize(second);
        var result = BackupJson.DeserializeAndValidate(firstJson);

        Assert.Equal(firstJson, secondJson);
        Assert.True(result.IsValid);
        Assert.NotNull(result.Document);
        Assert.Equal(first.Checksum, result.Document!.Checksum);
    }

    [Fact]
    public void Deserialize_reports_malformed_json_without_throwing()
    {
        var result = BackupJson.DeserializeAndValidate("{not-json");

        Assert.False(result.IsValid);
        Assert.Null(result.Document);
        Assert.Contains(result.Errors, error => error.Contains("JSON", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Deserialize_rejects_checksum_tampering()
    {
        var json = BackupJson.Serialize(CreateDocument());
        var tampered = json.Replace("Bench Press", "Incline Press", StringComparison.Ordinal);

        var result = BackupJson.DeserializeAndValidate(tampered);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("checksum", StringComparison.OrdinalIgnoreCase));
    }

    private static BackupDocument CreateDocument(bool reverseExercises = false)
    {
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var exercises = new[]
        {
            new Exercise(firstId, "Bench Press", ExerciseType.Weight, MeasurementUnit.Kilograms, true),
            new Exercise(secondId, "Squat", ExerciseType.Weight, MeasurementUnit.Kilograms, true)
        };

        return BackupJson.CreateDocument(
            reverseExercises ? exercises.Reverse().ToArray() : exercises,
            [], [], [], [], [], [], [], null,
            new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero));
    }
}
