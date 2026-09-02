# Activity Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add offline manual activity logging for walking, running, and swimming with local persistence and weekly summaries.

**Architecture:** Keep activities as a separate domain/application flow. Add a validated application service for pace and summaries, a parameterized SQLite repository behind an interface, and thin MAUI pages that consume those boundaries through dependency injection.

**Tech Stack:** .NET 10, C#, .NET MAUI, Microsoft.Data.Sqlite, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-01-issue-6-activity-logging-design.md`

## Global Constraints

- Activities are limited to walking, running, and swimming.
- Date and activity type are required; duration, distance, steps, and notes are optional.
- Pace is calculated only for walking and running when duration and distance are present.
- Activity records remain separate from gym workout sessions.
- All persistence remains local and parameterized.

---

### Task 1: Activity rules and summaries

**Files:**
- Create: `src/GymTracker.Core/Application/ActivityLogging.cs`
- Test: `tests/GymTracker.Tests/ActivityLoggingTests.cs`

**Interfaces:**
- `ActivityType` enum with `Walking`, `Running`, `Swimming`.
- `ActivityEntry` record with date, type, optional duration/distance/steps/notes.
- `ActivityLogging.CalculatePace(ActivityEntry)` returns nullable `TimeSpan`.
- `ActivityLogging.GetWeeklySummary(IEnumerable<ActivityEntry>, DateOnly weekStart)` returns totals and frequency.

- [ ] Write failing tests for validation, pace, and weekly aggregation.
- [ ] Run the focused tests and confirm they fail because the API is absent.
- [ ] Implement the minimal validated records and pure calculations.
- [ ] Run focused tests and confirm they pass.

### Task 2: SQLite repository

**Files:**
- Modify: `src/GymTracker.Core/GymTracker.Core.csproj`
- Create: `src/GymTracker.Core/Infrastructure/IActivityRepository.cs`
- Create: `src/GymTracker.Core/Infrastructure/SqliteActivityRepository.cs`
- Modify: `src/GymTracker.Core/Infrastructure/DatabaseMigrations.cs`
- Test: `tests/GymTracker.Tests/SqliteActivityRepositoryTests.cs`

**Interfaces:**
- `InitializeAsync(CancellationToken)`.
- `AddAsync(ActivityEntry, CancellationToken)`.
- `ListAsync(DateOnly from, DateOnly to, CancellationToken)`.

- [ ] Add the SQLite dependency and write failing repository tests.
- [ ] Run repository tests to confirm the missing implementation/dependency failure.
- [ ] Implement schema initialization, parameterized insert, and date-range query.
- [ ] Run repository tests and the schema suite.

### Task 3: MAUI composition and activity UI

**Files:**
- Modify: `src/GymTracker/MauiProgram.cs`
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Test: `tests/GymTracker.Tests/ActivityLoggingTests.cs`

- [ ] Add the repository and activity service to dependency injection.
- [ ] Replace placeholder activity log content with entry controls and saved summaries.
- [ ] Display validation/storage feedback and calculated pace.
- [ ] Run the complete solution test suite and `git diff --check`.
