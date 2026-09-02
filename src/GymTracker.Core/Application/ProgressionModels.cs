namespace GymTracker.Application;

public enum RecommendationConfidence
{
    Low,
    Medium,
    High
}

public sealed record ProgressionRecommendationRequest(
    Guid ExerciseId,
    string ExerciseName,
    int TargetRepetitions,
    decimal WeightIncrementKg,
    IReadOnlyList<ProgressionHistoryEntry> History,
    bool IsWeighted = true,
    bool RecentlyUnused = false,
    decimal? FallbackWeightKg = null,
    int? FallbackRepetitions = null);

public sealed record ProgressionHistoryEntry(
    DateTimeOffset CompletedAt,
    decimal? WeightKg,
    int Repetitions,
    string Status,
    string? Notes = null);

public sealed record ProgressionRecommendation(
    decimal? ProposedWeightKg,
    int ProposedRepetitions,
    RecommendationConfidence Confidence,
    string RuleKey,
    string Explanation,
    string SafetyDisclaimer);
