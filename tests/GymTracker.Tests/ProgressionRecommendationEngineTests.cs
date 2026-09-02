using GymTracker.Application;
using Microsoft.Extensions.DependencyInjection;

namespace GymTracker.Tests;

public sealed class ProgressionRecommendationEngineTests
{
    private static ProgressionRecommendationEngine Engine => new();

    [Fact]
    public void Contracts_expose_the_engine_result_fields()
    {
        var result = new ProgressionRecommendation(60, 8, RecommendationConfidence.High,
            "increase-repetitions", "Repeat 60 kg for 8 reps.", ProgressionRecommendationEngine.SafetyDisclaimer);

        Assert.Equal(60, result.ProposedWeightKg);
        Assert.Equal(RecommendationConfidence.High, result.Confidence);
        Assert.Equal("increase-repetitions", result.RuleKey);
        Assert.Contains("general fitness", result.SafetyDisclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Increases_repetitions_until_the_target_is_reached()
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m, history: History(60, 8)))!;

        Assert.Equal(60, result.ProposedWeightKg);
        Assert.Equal(9, result.ProposedRepetitions);
        Assert.Equal(RecommendationConfidence.Medium, result.Confidence);
        Assert.Equal("increase-repetitions", result.RuleKey);
    }

    [Fact]
    public void Increases_weight_and_resets_repetitions_at_the_target()
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m, history: History(60, 10)))!;

        Assert.Equal(62.5m, result.ProposedWeightKg);
        Assert.Equal(1, result.ProposedRepetitions);
        Assert.Equal("increase-weight", result.RuleKey);
    }

    [Theory]
    [InlineData("Pain in left shoulder")]
    [InlineData("Joint pain")]
    public void Pain_prevents_increases(string notes)
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m, history: History(60, 10, notes)))!;

        Assert.Equal(60, result.ProposedWeightKg);
        Assert.Equal(10, result.ProposedRepetitions);
        Assert.Equal(RecommendationConfidence.Low, result.Confidence);
        Assert.Equal("hold-pain", result.RuleKey);
    }

    [Theory]
    [InlineData("Very hard")]
    [InlineData("RPE 10")]
    public void High_difficulty_prevents_increases(string notes)
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m, history: History(60, 10, notes)))!;

        Assert.Equal("hold-difficulty", result.RuleKey);
        Assert.Equal(RecommendationConfidence.Low, result.Confidence);
    }

    [Fact]
    public void Bodyweight_exercises_progress_repetitions_without_a_weight_increment()
    {
        var result = Engine.Recommend(Request(target: 12, weightIncrement: 0, isWeighted: false, history: History(null, 12)))!;

        Assert.Null(result.ProposedWeightKg);
        Assert.Equal(13, result.ProposedRepetitions);
    }

    [Fact]
    public void Ignores_incomplete_entries_and_uses_the_latest_successful_value()
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
            history: [History(60, 8, status: "Incomplete"), History(50, 8)]))!;

        Assert.Equal(50, result.ProposedWeightKg);
        Assert.Equal(9, result.ProposedRepetitions);
    }

    [Fact]
    public void Recently_unused_exercises_fall_back_to_the_last_successful_value()
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
            recentlyUnused: true, history: History(60, 8)))!;

        Assert.Equal(60, result.ProposedWeightKg);
        Assert.Equal(8, result.ProposedRepetitions);
        Assert.Equal(RecommendationConfidence.Low, result.Confidence);
        Assert.Equal("fallback-last-success", result.RuleKey);
    }

    [Fact]
    public void No_successful_history_returns_no_recommendation()
    {
        var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m, history: History(60, 8, status: "Skipped")));

        Assert.Null(result);
    }

    [Fact]
    public void Invalid_requests_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => Engine.Recommend(Request(name: " ")));
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Recommend(Request(target: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Recommend(Request(target: 10, weightIncrement: 0)));
    }

    [Fact]
    public void Application_services_register_the_progression_engine()
    {
        using var provider = new ServiceCollection()
            .AddGymTrackerApplication()
            .BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ProgressionRecommendationEngine>());
    }

    private static ProgressionRecommendationRequest Request(
        int target = 10,
        decimal weightIncrement = 2.5m,
        bool isWeighted = true,
        bool recentlyUnused = false,
        string name = "Bench Press",
        params ProgressionHistoryEntry[] history) =>
        new(Guid.NewGuid(), name, target, weightIncrement, history, isWeighted, recentlyUnused);

    private static ProgressionHistoryEntry History(
        decimal? weight,
        int repetitions,
        string? notes = null,
        string status = "Completed") =>
        new(DateTimeOffset.UtcNow, weight, repetitions, status, notes);
}
