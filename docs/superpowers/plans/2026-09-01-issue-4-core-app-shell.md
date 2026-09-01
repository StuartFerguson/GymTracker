# Core App Shell And Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a navigable .NET MAUI first-release shell with distinct destinations and a usable start-workout, active-workout, summary, and activity flow.

**Architecture:** Keep route names and workflow state in the platform-neutral core library. Implement lightweight MAUI pages in a single feature-page file with explicit page types, and register the pages through Shell so every acceptance destination is directly reachable. Use in-memory sample content only; persistence belongs to later issues.

**Tech Stack:** .NET SDK 10.0.400, .NET MAUI, C#, Shell, xUnit.

**Spec:** GitHub issue #4 acceptance criteria.

## Global Constraints

- Dashboard, Weekly Plan, Start Workout, Active Workout, Workout Summary, Activity Log, History, Exercise Progress, and Backup and Settings are reachable.
- The navigation flow supports the core workout and activity journeys.
- Set entry must remain a direct, low-friction interaction.
- No backend, account flow, or persistence is introduced in this shell issue.

---

### Task 1: Define the navigation and workout workflow contracts

**Files:**
- Modify: `src/GymTracker.Core/Application/AppRoutes.cs`
- Create: `src/GymTracker.Core/Application/WorkoutSession.cs`
- Modify: `tests/GymTracker.Tests/ScaffoldTests.cs`

- [ ] Add tests for the complete route set and recording a completed set.
- [ ] Run the tests and confirm they fail for the missing workflow behavior.
- [ ] Implement the smallest route metadata and in-memory workout session model.
- [ ] Run the core test project and confirm it passes.

### Task 2: Build the Shell destinations

**Files:**
- Create: `src/GymTracker/Pages/AppPages.cs`
- Modify: `src/GymTracker/AppShell.xaml`
- Modify: `src/GymTracker/AppShell.xaml.cs`
- Modify: `src/GymTracker/MainPage.xaml`
- Modify: `src/GymTracker/MainPage.xaml.cs`

- [ ] Add distinct page types for all nine first-release destinations.
- [ ] Add direct Shell flyout access for dashboard, plan, log, history, progress, and settings.
- [ ] Add direct navigation cards from the dashboard to every supporting destination.
- [ ] Add static route registration for start workout, active workout, and summary.

### Task 3: Implement the core workout and activity journeys

**Files:**
- Modify: `src/GymTracker/Pages/AppPages.cs`

- [ ] Make Start Workout open an Active Workout session.
- [ ] Make Active Workout accept weight and reps with an add-set action and show entered sets immediately.
- [ ] Make Finish Workout open Workout Summary with total sets and volume.
- [ ] Make the summary offer Activity Log navigation and return to the dashboard.

### Task 4: Verify the solution

- [ ] Run `dotnet test GymTracker.slnx`.
- [ ] Run `dotnet build GymTracker.slnx --no-restore`.
- [ ] Inspect the diff and confirm no persistence or backend dependency was added.
