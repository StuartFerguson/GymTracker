# Workout Session Logging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add low-friction workout set logging with optional data, reusable previous values, and a separate set-edit screen.

**Architecture:** Keep workout state in the existing in-memory application model and navigation state. Extend `WorkoutSet` with optional weight, notes, status, and per-dumbbell metadata; expose validated add/update operations. Keep the active page optimized for entry and add a registered `EditWorkoutSetPage` route for editing one set.

**Tech Stack:** .NET 10, .NET MAUI, C#, Shell, xUnit.

**Spec:** GitHub issue #5 acceptance criteria.

## Global Constraints

- Record weight, repetitions, and optional notes per set.
- Bodyweight exercises may omit weight.
- Dumbbell entries preserve a per-dumbbell convention.
- Failed, skipped, and incomplete sets remain recordable.
- Recorded sets are editable after entry.
- Previous-session values can be applied as starting values.
- No persistence or backend dependency is introduced.

---

### Task 1: Extend the workout session contract

**Files:**
- Modify: `src/GymTracker.Core/Application/WorkoutSession.cs`
- Modify: `tests/GymTracker.Tests/ScaffoldTests.cs`

- [x] Add failing tests for optional weight, notes, statuses, per-dumbbell values, editing, and previous-set starting values.
- [x] Run the focused tests and confirm they fail because the new contract is absent.
- [x] Implement the minimal validated model and operations.
- [x] Run the focused tests and confirm they pass.

### Task 2: Add the separate edit route and screen

**Files:**
- Modify: `src/GymTracker/AppShell.xaml.cs`
- Modify: `src/GymTracker/Pages/AppPages.cs`

- [x] Register `EditWorkoutSet` as a Shell route.
- [x] Add edit navigation state carrying the selected set index.
- [x] Add `EditWorkoutSetPage` with weight, reps, notes, status, Save changes, and Cancel actions.
- [x] Refresh the active page after a successful save and keep Cancel non-mutating.

### Task 3: Make active workout entry issue-complete

**Files:**
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Modify: `src/GymTracker.Core/Application/AppRoutes.cs`

- [x] Add optional notes and a bodyweight-friendly entry path.
- [x] Preserve the per-dumbbell convention for dumbbell exercises.
- [x] Add status selection for failed, skipped, and incomplete sets.
- [x] Add a `Use last session` action that fills the current exercise's starting values.
- [x] Render logged sets with an Edit action that opens the separate edit page.

### Task 4: Verify the complete change

- [x] Run `dotnet test GymTracker.slnx`.
- [x] Run `dotnet build GymTracker.slnx --no-restore`.
- [x] Inspect the diff for unintended persistence, route, or unrelated UI changes.
