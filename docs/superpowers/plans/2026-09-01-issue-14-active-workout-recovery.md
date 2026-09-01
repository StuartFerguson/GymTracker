# Active Workout Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist and restore an in-progress workout through unexpected app termination using an atomic local JSON snapshot.

**Architecture:** Keep workout reconstruction and snapshot contracts in `GymTracker.Core`. Put the file-backed implementation behind `IActiveWorkoutStore`, with an injected path so tests use temporary files and the MAUI app uses `FileSystem.AppDataDirectory`. The UI will save after successful set mutations, offer recovery from the start-workout page, and clear the snapshot on finish or cancel.

**Tech Stack:** .NET 10, C#, `System.Text.Json`, .NET file I/O, .NET MAUI dependency injection, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-01-issue-14-active-workout-recovery-design.md`

## Global Constraints

- Persist only the active workout snapshot; completed history remains out of scope.
- Use an atomic temporary-file replacement so an interrupted save cannot produce a partial valid snapshot.
- Preserve all existing `WorkoutSession` validation and snapshot fields.
- Use cancellation tokens on every asynchronous store operation.
- Do not add a SQLite provider or migrations.

---

### Task 1: Add the active-workout snapshot contract

**Files:**
- Modify: `src/GymTracker.Core/Application/WorkoutSession.cs`
- Create: `src/GymTracker.Core/Application/ActiveWorkoutSnapshot.cs`
- Test: `tests/GymTracker.Tests/ActiveWorkoutSnapshotTests.cs`

**Interfaces:**
- Produces `ActiveWorkoutSnapshot(string SessionName, string? TemplateName, IReadOnlyList<WorkoutSetSnapshot> Sets)`.
- Produces `WorkoutSession.ToSnapshot(string? templateName)` and `WorkoutSession.FromSnapshot(ActiveWorkoutSnapshot snapshot)`.

- [ ] **Step 1: Write the failing round-trip test**

```csharp
[Fact]
public void Snapshot_round_trip_preserves_all_active_set_values()
{
    var original = new WorkoutSession("Push");
    original.AddSet("Dumbbell Bench Press", 22.5m, 8, "last hard set", WorkoutSetStatus.Incomplete, true);
    original.AddSet("Pull Up", null, 0, "missed", WorkoutSetStatus.Failed);

    var restored = WorkoutSession.FromSnapshot(original.ToSnapshot("Push"));

    Assert.Equal("Push", restored.Name);
    Assert.Equal(original.Sets, restored.Sets);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --no-restore --filter FullyQualifiedName~ActiveWorkoutSnapshotTests`

Expected: FAIL because `ActiveWorkoutSnapshot`, `ToSnapshot`, and `FromSnapshot` do not exist.

- [ ] **Step 3: Implement the snapshot records and conversion methods**

Define `WorkoutSetSnapshot` with `Exercise`, `Weight`, `Reps`, `Notes`, `Status`, and `IsPerDumbbell`. Make `ToSnapshot` copy every current set and `FromSnapshot` create a session with the snapshot name, then re-add each set through `AddSet` so existing validation remains active. Reject null snapshots and blank session names.

- [ ] **Step 4: Run the focused test and verify it passes**

Run the same focused command; expected result: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/GymTracker.Core/Application/WorkoutSession.cs src/GymTracker.Core/Application/ActiveWorkoutSnapshot.cs tests/GymTracker.Tests/ActiveWorkoutSnapshotTests.cs
git commit -m "feat: add active workout snapshot contract"
```

### Task 2: Implement the atomic JSON store

**Files:**
- Create: `src/GymTracker.Core/Infrastructure/IActiveWorkoutStore.cs`
- Create: `src/GymTracker.Core/Infrastructure/JsonActiveWorkoutStore.cs`
- Test: `tests/GymTracker.Tests/JsonActiveWorkoutStoreTests.cs`

**Interfaces:**
- `Task SaveAsync(ActiveWorkoutSnapshot snapshot, CancellationToken cancellationToken = default)`.
- `Task<ActiveWorkoutSnapshot?> LoadAsync(CancellationToken cancellationToken = default)`.
- `Task ClearAsync(CancellationToken cancellationToken = default)`.
- `JsonActiveWorkoutStore(string filePath, JsonSerializerOptions? options = null)`.

- [ ] **Step 1: Write the failing store tests**

```csharp
[Fact]
public async Task Store_round_trip_preserves_a_snapshot()
{
    var path = Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.json");
    var store = new JsonActiveWorkoutStore(path);
    var snapshot = new ActiveWorkoutSnapshot("Pull", "Pull", [
        new WorkoutSetSnapshot("Pull Up", null, 0, "failed rep", WorkoutSetStatus.Failed, false)]);

    await store.SaveAsync(snapshot);

    Assert.Equal(snapshot, await store.LoadAsync());
}

[Fact]
public async Task Malformed_snapshot_is_removed_and_returns_no_state()
{
    var path = Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.json");
    await File.WriteAllTextAsync(path, "{not-json");
    var store = new JsonActiveWorkoutStore(path);

    Assert.Null(await store.LoadAsync());
    Assert.False(File.Exists(path));
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --no-restore --filter FullyQualifiedName~JsonActiveWorkoutStoreTests`

Expected: FAIL because the store interface and implementation do not exist.

- [ ] **Step 3: Implement save, load, and clear**

Serialize with `System.Text.Json` to `<filePath>.tmp`, flush the file with `Flush(true)`, close it, then move it over the target with overwrite enabled. On load, return null for a missing file; on JSON or I/O read failure, attempt to delete the invalid snapshot and return null. `ClearAsync` deletes the target if present and tolerates an already-missing file.

- [ ] **Step 4: Add replacement and clear tests, then run all store tests**

Add a test that saves snapshot A, saves snapshot B, and confirms load returns B; add a test that clear leaves no file. Run the focused command and expect all store tests to pass.

- [ ] **Step 5: Commit**

```powershell
git add src/GymTracker.Core/Infrastructure/IActiveWorkoutStore.cs src/GymTracker.Core/Infrastructure/JsonActiveWorkoutStore.cs tests/GymTracker.Tests/JsonActiveWorkoutStoreTests.cs
git commit -m "feat: persist active workout snapshots atomically"
```

### Task 3: Wire storage into MAUI composition and recovery

**Files:**
- Modify: `src/GymTracker/MauiProgram.cs`
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Test: `tests/GymTracker.Tests/ActiveWorkoutRecoveryTests.cs`

**Interfaces:**
- Registers one `IActiveWorkoutStore` using `Path.Combine(FileSystem.AppDataDirectory, "active-workout.json")`.
- Adds a small page-facing recovery coordinator with `LoadAsync`, `SaveAsync`, `ClearAsync`, and `Resume` behavior backed by the store.

- [ ] **Step 1: Write the failing recovery test**

```csharp
[Fact]
public async Task Recovery_coordinator_restores_the_saved_session()
{
    var path = Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.json");
    var store = new JsonActiveWorkoutStore(path);
    var session = new WorkoutSession("Legs");
    session.AddSet("Back Squat", 80, 6, "felt strong");
    var coordinator = new ActiveWorkoutRecovery(store);

    await coordinator.SaveAsync(session, "Legs");
    var recovery = await coordinator.LoadAsync();

    Assert.NotNull(recovery);
    Assert.Equal(session.Sets, recovery!.Session.Sets);
    Assert.Equal("Legs", recovery.TemplateName);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --no-restore --filter FullyQualifiedName~ActiveWorkoutRecoveryTests`

Expected: FAIL because the recovery coordinator does not exist.

- [ ] **Step 3: Implement composition and coordinator**

Register the store as a singleton in `MauiProgram`. Keep the coordinator independent of MAUI controls and expose a recovered pair of `WorkoutSession` plus nullable template name. Resolve it from the app service provider in page code, preserving the existing Shell page construction pattern.

- [ ] **Step 4: Run the focused test and verify it passes**

Run the focused command; expected result: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/GymTracker/MauiProgram.cs src/GymTracker/Pages/AppPages.cs tests/GymTracker.Tests/ActiveWorkoutRecoveryTests.cs
git commit -m "feat: add active workout recovery service"
```

### Task 4: Persist mutations and clear terminal states

**Files:**
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Modify: `tests/GymTracker.Tests/ActiveWorkoutRecoveryTests.cs`

**Interfaces:**
- Successful add/edit operations call `SaveAsync` with the current session and template name.
- Finish and cancel call `ClearAsync` after navigation succeeds.
- Start Workout offers resume, discard, and normal template/quick-start paths when a snapshot exists.

- [ ] **Step 1: Add failing behavior tests for clear-on-finish/cancel**

Extend the recovery coordinator tests to save a snapshot, call `ClearAsync`, and assert that `LoadAsync` returns null. Add a test that a recovered failed/skipped/incomplete set and notes remain unchanged after restore.

- [ ] **Step 2: Run the recovery tests and verify the new assertions fail**

Run: `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --no-restore --filter FullyQualifiedName~ActiveWorkoutRecoveryTests`

Expected: FAIL for the missing terminal-state integration.

- [ ] **Step 3: Implement UI persistence and recovery actions**

Load the snapshot from `StartWorkoutPage.OnAppearing`, add a resume action that restores `WorkoutNavigationState`, and add a discard action that clears it. Save after `AddSet` and after `EditWorkoutSetPage` successfully updates a set. Add a cancel action to the active page and clear after finish/cancel navigation. Catch store exceptions, keep the in-memory session, and show a concise feedback message.

- [ ] **Step 4: Run the full verification suite**

Run: `dotnet test GymTracker.slnx --no-restore`

Expected: 0 failures, with all existing and new tests passing and all MAUI target builds completing.

- [ ] **Step 5: Commit**

```powershell
git add src/GymTracker/Pages/AppPages.cs tests/GymTracker.Tests/ActiveWorkoutRecoveryTests.cs
git commit -m "feat: recover active workouts after restart"
```

### Final verification

- [ ] Run `git diff --check`.
- [ ] Run `dotnet test GymTracker.slnx --no-restore` and record the test count and exit code.
- [ ] Confirm `git status --short` contains no unintended files.
