namespace GymTracker.Application;

public sealed class ProgressionRecommendationEngine
{
    public const string SafetyDisclaimer =
        "This is general fitness guidance, not medical advice. Stop if you feel pain and consult a qualified health professional when needed.";

    private static readonly string[] PainIndicators = ["pain", "injury", "hurt"];
    private static readonly string[] DifficultyIndicators = ["very hard", "too hard", "max effort", "rpe 9", "rpe 10"];

    public ProgressionRecommendation? Recommend(ProgressionRecommendationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExerciseId == Guid.Empty) throw new ArgumentException("An exercise is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ExerciseName)) throw new ArgumentException("An exercise name is required.", nameof(request));
        if (request.TargetRepetitions <= 0) throw new ArgumentOutOfRangeException(nameof(request), "The target repetitions must be positive.");
        if (request.IsWeighted && request.WeightIncrementKg <= 0) throw new ArgumentOutOfRangeException(nameof(request), "The weight increment must be positive for weighted exercises.");
        if (request.FallbackRepetitions is <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Fallback repetitions must be positive when supplied.");
        if (request.FallbackWeightKg is <= 0) throw new ArgumentOutOfRangeException(nameof(request), "Fallback weight must be positive when supplied.");
        ArgumentNullException.ThrowIfNull(request.History);

        var latest = request.History
            .Where(entry => string.Equals(entry.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.CompletedAt)
            .FirstOrDefault();
        if (latest is null)
        {
            return CreateNewExerciseFallback(request);
        }

        if (ContainsIndicator(latest.Notes, PainIndicators))
        {
            return Create(latest.WeightKg, latest.Repetitions, RecommendationConfidence.Low, "hold-pain",
                $"Repeat {FormatValue(latest)} because the latest notes mention pain or injury.");
        }

        if (ContainsIndicator(latest.Notes, DifficultyIndicators))
        {
            return Create(latest.WeightKg, latest.Repetitions, RecommendationConfidence.Low, "hold-difficulty",
                $"Repeat {FormatValue(latest)} because the latest set was reported as very difficult.");
        }

        if (request.RecentlyUnused)
        {
            return Create(latest.WeightKg, latest.Repetitions, RecommendationConfidence.Low, "fallback-last-success",
                $"Return to the last successful value of {FormatValue(latest)} because this exercise has not been used recently.");
        }

        if (latest.Repetitions < request.TargetRepetitions)
        {
            return Create(latest.WeightKg, latest.Repetitions + 1, RecommendationConfidence.Medium, "increase-repetitions",
                $"Try {FormatValue(latest.WeightKg, latest.Repetitions + 1)} to build toward the target of {request.TargetRepetitions} repetitions.");
        }

        if (!request.IsWeighted)
        {
            return Create(null, latest.Repetitions + 1, RecommendationConfidence.Medium, "increase-repetitions",
                $"Try {latest.Repetitions + 1} repetitions; bodyweight exercises do not automatically increase weight.");
        }

        var nextWeight = latest.WeightKg is null ? null : latest.WeightKg + request.WeightIncrementKg;
        return Create(nextWeight, 1, RecommendationConfidence.Medium, "increase-weight",
            nextWeight is null
                ? "Repeat the exercise with a recorded weight before increasing load."
                : $"Try {FormatValue(nextWeight, 1)} after completing the target of {request.TargetRepetitions} repetitions.");
    }

    private static ProgressionRecommendation Create(decimal? weight, int repetitions, RecommendationConfidence confidence, string ruleKey, string explanation) =>
        new(weight, repetitions, confidence, ruleKey, explanation, SafetyDisclaimer);

    private static ProgressionRecommendation? CreateNewExerciseFallback(ProgressionRecommendationRequest request)
    {
        if (request.FallbackRepetitions is null || (request.IsWeighted && request.FallbackWeightKg is null)) return null;

        return Create(request.IsWeighted ? request.FallbackWeightKg : null, request.FallbackRepetitions.Value,
            RecommendationConfidence.Low, "fallback-new-exercise",
            $"Start with {FormatValue(request.IsWeighted ? request.FallbackWeightKg : null, request.FallbackRepetitions.Value)} on the first use because there is no successful history for this exercise.");
    }

    private static bool ContainsIndicator(string? notes, IEnumerable<string> indicators) =>
        !string.IsNullOrWhiteSpace(notes) && indicators.Any(indicator => notes.Contains(indicator, StringComparison.OrdinalIgnoreCase));

    private static string FormatValue(ProgressionHistoryEntry entry) => FormatValue(entry.WeightKg, entry.Repetitions);

    private static string FormatValue(decimal? weight, int repetitions) =>
        weight is null ? $"bodyweight for {repetitions} reps" : $"{weight:g} kg for {repetitions} reps";
}
