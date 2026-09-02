namespace GymTracker.Application;

public sealed record WorkoutHistorySummary(
    DateTimeOffset StartedAt,
    string Name,
    int CompletedSetCount,
    int PlannedSetCount,
    int Repetitions,
    decimal TrainingVolumeKg);

public sealed record ProgressMetrics(
    int WorkoutCount,
    int TotalSets,
    int TotalRepetitions,
    decimal TrainingVolumeKg,
    int ConsistentWeeks);

public sealed record ActivityHistorySummary(
    DateOnly Date,
    ActivityType Type,
    int DurationSeconds,
    decimal DistanceMetres);

public sealed record HistoryReport(
    IReadOnlyList<WorkoutHistorySummary> Workouts,
    ProgressMetrics Metrics,
    IReadOnlyList<ActivityHistorySummary> Activities);

public sealed record ExerciseProgressSummary(
    string ExerciseName,
    decimal? BestWeightKg,
    int BestRepetitions,
    IReadOnlyList<ExerciseProgressEntry> Entries);

public sealed record ExerciseProgressEntry(
    DateTimeOffset Date,
    decimal? WeightKg,
    int Repetitions);
