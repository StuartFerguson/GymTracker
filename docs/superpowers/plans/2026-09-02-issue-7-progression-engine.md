# Issue #7 Progression Recommendation Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a deterministic, explainable, independently testable service that recommends the next weight and repetition target from completed workout history.

**Architecture:** Add immutable request, history-entry, confidence, and result records plus a side-effect-free `ProgressionRecommendationEngine` under `GymTracker.Core.Application`. The engine applies explicit ordered rules and returns a result; it does not persist data or mutate workout plans. Register it in the existing application dependency-injection extension for future UI callers.

**Tech Stack:** C#, .NET, xUnit, existing `GymTracker.Core` application/domain projects.

**Spec:** `docs/superpowers/specs/2026-09-02-issue-7-progression-engine-design.md`

## Global Constraints

- Recommendations are deterministic and explainable.
- Pain and high difficulty prevent automatic increases.
- New or recently unused exercises use the last successful value with low confidence.
- The engine is platform-independent and independently testable.
- Recommendations are guidance, not medical advice; every result includes the safety disclaimer.

---

### Task 1: Define the progression contracts

**Files:**
- Create: `src/GymTracker.Core/Application/ProgressionModels.cs`
- Test: `tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs`

**Interfaces:**
- Produce `ProgressionRecommendationRequest(Guid ExerciseId, string ExerciseName, int TargetRepetitions, decimal WeightIncrementKg, IReadOnlyList<ProgressionHistoryEntry> History, bool IsWeighted = true, bool RecentlyUnused = false)`.
- Produce `ProgressionHistoryEntry(DateTimeOffset CompletedAt, decimal? WeightKg, int Repetitions, string Status, string? Notes = null)`.
- Produce `ProgressionRecommendation(decimal? ProposedWeightKg, int ProposedRepetitions, RecommendationConfidence Confidence, string RuleKey, string Explanation, string SafetyDisclaimer)`.
- Produce `RecommendationConfidence` values `Low`, `Medium`, and `High`.

- [ ] **Step 1: Write the failing contract/behavior tests**

```csharp
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
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: FAIL because the progression contracts and engine do not exist.

- [ ] **Step 3: Add the immutable records and enum**

Implement the exact records and enum above. Keep validation in the engine so the records remain simple data contracts.

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/GymTracker.Core/Application/ProgressionModels.cs tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs
git commit -m "feat: define progression recommendation contracts"
```

### Task 2: Implement repetition-first progression

**Files:**
- Create: `src/GymTracker.Core/Application/ProgressionRecommendationEngine.cs`
- Modify: `tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs`

**Interfaces:**
- Produce `ProgressionRecommendationEngine.Recommend(ProgressionRecommendationRequest request)`.
- `Recommend` filters history to `Status == "Completed"` case-insensitively, orders by `CompletedAt` descending, and uses the newest successful entry.

- [ ] **Step 1: Write failing tests for below-target and target-achieved behavior**

```csharp
[Fact]
public void Increases_repetitions_until_the_target_is_reached()
{
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        History(60, 8)));

    Assert.Equal(60, result.ProposedWeightKg);
    Assert.Equal(9, result.ProposedRepetitions);
    Assert.Equal(RecommendationConfidence.High, result.Confidence);
    Assert.Equal("increase-repetitions", result.RuleKey);
}

[Fact]
public void Increases_weight_and_resets_repetitions_at_the_target()
{
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        History(60, 10)));

    Assert.Equal(62.5m, result.ProposedWeightKg);
    Assert.Equal(1, result.ProposedRepetitions);
    Assert.Equal("increase-weight", result.RuleKey);
}
```

- [ ] **Step 2: Run tests to verify they fail for the missing implementation**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: FAIL because `Recommend` is not implemented.

- [ ] **Step 3: Implement the minimal deterministic engine**

Validate non-empty exercise ID/name, positive target repetitions, and positive increment when `IsWeighted` is true. Require at least one completed history entry. For the newest completed entry, propose `repetitions + 1` at the same weight when below target; otherwise propose `weight + WeightIncrementKg` and one repetition. Use `Medium` confidence for normal history-driven results and include the exact rule key and explanation.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/GymTracker.Core/Application/ProgressionRecommendationEngine.cs tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs
git commit -m "feat: add repetition-first progression rules"
```

### Task 3: Add safety gates and fallback behavior

**Files:**
- Modify: `src/GymTracker.Core/Application/ProgressionRecommendationEngine.cs`
- Modify: `tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs`

**Interfaces:**
- Preserve `Recommend` and the result contract from Task 2.
- Use rule keys `hold-pain`, `hold-difficulty`, `fallback-last-success`, `increase-repetitions`, and `increase-weight`.

- [ ] **Step 1: Write failing tests for safety, fallback, bodyweight, filtering, and validation**

```csharp
[Theory]
[InlineData("Pain in left shoulder")]
[InlineData("Joint pain")]
public void Pain_prevents_increases(string notes)
{
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        History(60, 10, notes)));

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
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        History(60, 10, notes)));

    Assert.Equal("hold-difficulty", result.RuleKey);
    Assert.Equal(RecommendationConfidence.Low, result.Confidence);
}

[Fact]
public void Bodyweight_exercises_progress_repetitions_without_a_weight_increment()
{
    var result = Engine.Recommend(Request(target: 12, weightIncrement: 0, isWeighted: false,
        History(null, 12)));

    Assert.Null(result.ProposedWeightKg);
    Assert.Equal(13, result.ProposedRepetitions);
}

[Fact]
public void Ignores_incomplete_entries_and_uses_the_latest_successful_value()
{
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        History(60, 8, status: "Incomplete"), History(50, 8)));

    Assert.Equal(50, result.ProposedWeightKg);
    Assert.Equal(9, result.ProposedRepetitions);
}
```

Add these concrete cases as well:

```csharp
[Fact]
public void Recently_unused_exercises_fall_back_to_the_last_successful_value()
{
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        recentlyUnused: true, History(60, 8)));

    Assert.Equal(60, result.ProposedWeightKg);
    Assert.Equal(8, result.ProposedRepetitions);
    Assert.Equal(RecommendationConfidence.Low, result.Confidence);
    Assert.Equal("fallback-last-success", result.RuleKey);
}

[Fact]
public void No_successful_history_returns_no_recommendation()
{
    var result = Engine.Recommend(Request(target: 10, weightIncrement: 2.5m,
        History(60, 8, status: "Skipped")));

    Assert.Null(result);
}

[Fact]
public void Invalid_requests_are_rejected()
{
    Assert.Throws<ArgumentException>(() => Engine.Recommend(Request(name: " ")));
    Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Recommend(Request(target: 0)));
    Assert.Throws<ArgumentOutOfRangeException>(() => Engine.Recommend(Request(target: 10, weightIncrement: 0)));
}
```

Define the test fixture helpers used above as `Engine => new ProgressionRecommendationEngine()` and `History(decimal? weight, int repetitions, string? notes = null, string status = "Completed") => [new(DateTimeOffset.UtcNow, weight, repetitions, status, notes)]`; make `Request` accept the named overrides and construct a valid `ProgressionRecommendationRequest`.

- [ ] **Step 2: Run the tests to verify the new cases fail**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: FAIL for the unimplemented safety gates and fallback rules.

- [ ] **Step 3: Implement finite, case-insensitive note vocabularies**

Treat notes containing `pain`, `injury`, or `hurt` as pain indicators. Treat notes containing `very hard`, `too hard`, `max effort`, `RPE 9`, or `RPE 10` as high difficulty indicators. Check pain before difficulty, then hold the latest successful value with low confidence. For `IsWeighted == false`, never add weight; progress repetitions only. When `RecentlyUnused` is true, hold the latest successful value with low confidence and the fallback rule key. The optional final `RecentlyUnused` parameter is already part of the contract from Task 1.

- [ ] **Step 4: Run focused and full tests**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: PASS.

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj`

Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/GymTracker.Core/Application/ProgressionRecommendationEngine.cs src/GymTracker.Core/Application/ProgressionModels.cs tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs
git commit -m "feat: gate progression recommendations on safety signals"
```

### Task 4: Register the engine and verify the solution

**Files:**
- Modify: `src/GymTracker.Core/Application/ServiceCollectionExtensions.cs`
- Modify: `tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs`

- [ ] **Step 1: Write the failing DI registration test**

```csharp
[Fact]
public void Application_services_register_the_progression_engine()
{
    using var provider = new ServiceCollection()
        .AddGymTrackerApplication()
        .BuildServiceProvider();

    Assert.NotNull(provider.GetRequiredService<ProgressionRecommendationEngine>());
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --filter FullyQualifiedName~ProgressionRecommendationEngineTests`

Expected: FAIL because the engine is not registered.

- [ ] **Step 3: Register the singleton**

Add `services.AddSingleton<ProgressionRecommendationEngine>();` to `AddGymTrackerApplication` and add the required `Microsoft.Extensions.DependencyInjection` using in the test if the existing test file does not already import it.

- [ ] **Step 4: Run final verification**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj`

Expected: all tests PASS.

Run: `dotnet build GymTracker.slnx --no-restore`

Expected: solution builds successfully with no compilation errors.

- [ ] **Step 5: Commit**

```bash
git add src/GymTracker.Core/Application/ServiceCollectionExtensions.cs tests/GymTracker.Tests/ProgressionRecommendationEngineTests.cs
git commit -m "feat: register progression recommendation engine"
```
