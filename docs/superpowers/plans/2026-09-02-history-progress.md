# History and Progress Browsing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist completed workouts and expose truthful workout/activity history and progress metrics through the existing MAUI history screens.

**Architecture:** Add a focused `IWorkoutHistoryRepository` for completed workout records, backed by the existing SQLite schema. Add immutable progress query records and a `ProgressService` that aggregates repository data, then inject that service into the two existing placeholder pages. Keep route names and current write models intact.

**Tech Stack:** .NET 10, C#, .NET MAUI, Microsoft.Data.Sqlite, xUnit 2.9.

**Spec:** `docs/superpowers/specs/2026-09-02-history-progress-design.md`

## Global Constraints

- Date ranges use inclusive `DateOnly` boundaries.
- Workout metrics include only sets with `WorkoutSetStatus.Completed`.
- Existing schema and navigation routes remain compatible.
- Tests must be deterministic and must use the existing xUnit conventions.

---

### Task 1: Add workout history persistence contracts and tests

**Files:**
- Create: `src/GymTracker.Core/Infrastructure/IWorkoutHistoryRepository.cs`
- Create: `src/GymTracker.Core/Infrastructure/SqliteWorkoutHistoryRepository.cs`
- Modify: `src/GymTracker.Core/Application/WorkoutSession.cs`
- Test: `tests/GymTracker.Tests/SqliteWorkoutHistoryRepositoryTests.cs`

**Interfaces:**
- Produces `IWorkoutHistoryRepository.SaveAsync(WorkoutSessionRecord, IReadOnlyList<WorkoutSetRecord>, CancellationToken)` and `ListAsync(DateOnly from, DateOnly to, CancellationToken)`.

- [ ] Write tests that save a completed session with sets, list it inside an inclusive date range, preserve set order, and exclude sessions outside the range.
- [ ] Run the repository tests and confirm they fail because the repository contract/implementation is absent.
- [ ] Add the repository contract and SQLite implementation using the existing `workout_sessions` and `workout_sets` tables, creating the database schema through the existing initializer.
- [ ] Add a `WorkoutSession.ToRecord` conversion that assigns stable session/set IDs, stores completed-at time, and maps set status through the existing persistence model without changing the interactive session API.
- [ ] Run repository and existing core tests; confirm all pass.

### Task 2: Persist finished workouts from the MAUI flow

**Files:**
- Modify: `src/GymTracker/MauiProgram.cs`
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Test: `tests/GymTracker.Tests/WorkoutHistoryMappingTests.cs`

**Interfaces:**
- Consumes `IWorkoutHistoryRepository` from DI.
- Produces a saved `WorkoutSessionRecord` and `WorkoutSetRecord` collection when `FinishWorkout` completes.

- [ ] Add mapping tests for template name, start/completion timestamps, exercise snapshots, set numbers, weights, reps, notes, and unit values.
- [ ] Run the mapping tests and confirm they fail before the mapping exists.
- [ ] Register the SQLite workout-history repository beside `IActivityRepository` and call it from the finish flow before clearing recovery state.
- [ ] Preserve the existing user-visible error handling when persistence fails.
- [ ] Run all tests and build the MAUI project targets used by the repository CI.

### Task 3: Implement progress aggregation with failing-first tests

**Files:**
- Create: `src/GymTracker.Core/Application/ProgressModels.cs`
- Create: `src/GymTracker.Core/Application/ProgressService.cs`
- Modify: `src/GymTracker.Core/Application/ServiceCollectionExtensions.cs`
- Modify: `src/GymTracker.Core/Infrastructure/IWorkoutHistoryRepository.cs`
- Test: `tests/GymTracker.Tests/ProgressServiceTests.cs`

**Interfaces:**
- `ProgressService.GetHistoryAsync(DateOnly from, DateOnly to, CancellationToken)` returns session summaries, aggregate metrics, planned/completed counts, and activity history.
- `ProgressService.GetExerciseProgressAsync(DateOnly from, DateOnly to, CancellationToken)` returns one progression summary per exercise.

- [ ] Write tests for newest-first session ordering, completed-only sets, total sets/reps/volume, maximum weight/reps personal bests, weekly consistency, planned-vs-completed counts, and activity distance/duration.
- [ ] Run the tests and verify expected failures for missing service behavior.
- [ ] Implement immutable result records and aggregation using repository data plus `IActivityRepository`.
- [ ] Define personal best as the greatest completed weight, with repetitions as the tie-breaker; bodyweight/repetition-only exercises use greatest repetitions.
- [ ] Define weekly consistency as the count of distinct calendar weeks containing at least one completed workout in the requested range.
- [ ] Run the focused tests, then the complete test suite.

### Task 4: Replace placeholder history and progress pages

**Files:**
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Test: `tests/GymTracker.Tests/ScaffoldTests.cs` (only if route/page behavior assertions need extension)

**Interfaces:**
- Consumes `ProgressService` through the existing MAUI service provider.
- Produces populated `HistoryPage` and `ExerciseProgressPage` views with empty and error states.

- [ ] Add date-range controls and a refresh path to `HistoryPage`; render workout summaries, metrics, planned/completed counts, and activity distance/duration.
- [ ] Add exercise selection/list rendering to `ExerciseProgressPage`; render personal best and progression entries.
- [ ] Keep UI event handlers thin and catch the same persistence exceptions used elsewhere in the app.
- [ ] Build the app and run all tests.

### Task 5: Final verification

**Files:**
- No additional files unless verification exposes a defect.

- [ ] Run `dotnet test GymTracker.slnx` and confirm zero failures.
- [ ] Run `dotnet build src/GymTracker/GymTracker.csproj --configuration Release --framework net10.0-android --no-restore` when the Android workload is available.
- [ ] Review `git diff`, confirm no unrelated files changed, and report any platform build limitation explicitly.
