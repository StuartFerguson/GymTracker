namespace GymTracker.Domain;

public sealed record GymTrackerData
{
    public int SchemaVersion { get; init; }
    public List<WorkoutRecord> Workouts { get; init; } = [];
    public List<WorkoutTemplateRecord> Templates { get; init; } = [];
    public List<ExerciseCatalogueEntry> Exercises { get; init; } = [];
    public List<ActivityRecord> Activities { get; init; } = [];
    public List<RecommendationRecord> Recommendations { get; init; } = [];
    public List<SettingRecord> Settings { get; init; } = [];
    public List<BackupMetadataRecord> BackupMetadata { get; init; } = [];
}

public sealed record WorkoutRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
    public string TemplateNameSnapshot { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public List<WorkoutExerciseRecord> Exercises { get; init; } = [];
}

public sealed record WorkoutExerciseRecord
{
    public string ExerciseNameSnapshot { get; init; } = string.Empty;
    public double Weight { get; init; }
    public int Repetitions { get; init; }
    public int Sets { get; init; } = 1;
}

public sealed record WorkoutTemplateRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string DefinitionJson { get; init; } = "{}";
}

public sealed record ExerciseCatalogueEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string MuscleGroup { get; init; } = string.Empty;
}

public sealed record ActivityRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public int DurationMinutes { get; init; }
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RecommendationRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record SettingRecord
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed record BackupMetadataRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
