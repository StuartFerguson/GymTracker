# Versioned JSON Backup And Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export and safely restore all local GymTracker state through a validated, versioned JSON backup file.

**Architecture:** Keep a platform-neutral `BackupService` responsible for JSON, checksums, validation, and import orchestration. Add an `IBackupDataStore` SQLite adapter that reads and transactionally replaces or merges persisted rows, while the existing active-workout store supplies the separate recovery snapshot. Keep file pick/share and confirmation prompts in `BackupSettingsPage`.

**Tech Stack:** .NET 10, C#, .NET MAUI, `System.Text.Json`, `Microsoft.Data.Sqlite`, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-02-issue-9-backup-import-design.md`

## Global Constraints

- The current backup format is `1`; unsupported future versions are rejected without touching local data.
- The JSON uses camelCase property names and enum strings.
- Import validates the complete document before opening a write transaction.
- Replacement creates a timestamped recoverable SQLite copy before mutation.
- Replacement database mutation is one transaction and rolls back on failure.
- No account, network, cloud-backup, or unrelated refactoring is introduced.
- Production code is written only after a test has failed for the missing behavior.

---

### Task 1: Define the backup contract and validator

**Files:**
- Create: `src/GymTracker.Core/Application/BackupModels.cs`
- Create: `src/GymTracker.Core/Application/BackupValidation.cs`
- Test: `tests/GymTracker.Tests/BackupValidationTests.cs`

**Interfaces:**
- Produces `BackupDocument`, `BackupValidationResult`, `BackupImportMode`, and `BackupValidation.Validate(BackupDocument)`.
- `BackupDocument` contains `FormatVersion`, `ExportedAt`, `Checksum`, the nine persisted collections from the spec, and nullable `ActiveWorkout`.

- [x] **Step 1: Write failing validation tests** for format version, required collections, duplicate IDs, duplicate template positions, duplicate set numbers per session, orphaned references, invalid enum values, blank required strings, invalid timestamps, negative durations/distances/repetitions/weights, and valid documents.
- [x] **Step 2: Run the focused tests** with `dotnet test tests/GymTracker.Tests/GymTracker.Tests.csproj --no-restore --filter FullyQualifiedName~BackupValidationTests`; confirm they fail because the contract/validator is absent.
- [x] **Step 3: Add the immutable records and validator** with all validation errors accumulated in stable path-prefixed messages, including references from sets to sessions/exercises, template items to templates/exercises, and planned sessions to templates.
- [x] **Step 4: Re-run the focused tests** and confirm every validation case passes.
- [x] **Step 5: Commit** with `git add src/GymTracker.Core/Application/BackupModels.cs src/GymTracker.Core/Application/BackupValidation.cs tests/GymTracker.Tests/BackupValidationTests.cs` and `git commit -m "feat: add backup contract validation"`.

### Task 2: Add canonical JSON serialization and checksum verification

**Files:**
- Create: `src/GymTracker.Core/Application/BackupJson.cs`
- Modify: `tests/GymTracker.Tests/BackupValidationTests.cs`
- Test: `tests/GymTracker.Tests/BackupJsonTests.cs`

**Interfaces:**
- Produces `BackupJson.Serialize(BackupDocument)`, `BackupJson.DeserializeAndValidate(string)`, and `BackupJson.CreateDocument(...)`.
- `DeserializeAndValidate` returns a result containing either a validated document or all parse/checksum/structural errors and never throws for malformed user input.

- [x] **Step 1: Write failing tests** for camelCase JSON, enum-string JSON, deterministic collection ordering, round-trip equality, malformed JSON, missing collections, unsupported versions, and checksum tampering.
- [x] **Step 2: Run `dotnet test ... --filter FullyQualifiedName~BackupJsonTests`** and confirm failure due to missing serializer APIs.
- [x] **Step 3: Implement `System.Text.Json` options** with `JsonSerializerDefaults.Web`, string enum conversion, canonical sorting by IDs/timestamps, UTF-8 serialization, SHA-256 checksum over a copy with blank `Checksum`, and constant-time checksum comparison.
- [x] **Step 4: Re-run the focused JSON tests** and confirm they pass without changing the validation tests.
- [x] **Step 5: Commit** with `git add src/GymTracker.Core/Application/BackupJson.cs tests/GymTracker.Tests/BackupJsonTests.cs tests/GymTracker.Tests/BackupValidationTests.cs` and `git commit -m "feat: serialize and verify backup JSON"`.

### Task 3: Read the complete SQLite backup data set

**Files:**
- Create: `src/GymTracker.Core/Infrastructure/IBackupDataStore.cs`
- Create: `src/GymTracker.Core/Infrastructure/SqliteBackupDataStore.cs`
- Test: `tests/GymTracker.Tests/SqliteBackupDataStoreTests.cs`

**Interfaces:**
- `IBackupDataStore.ReadAsync(CancellationToken)` returns public `BackupDataSet`, a persistence-shaped record containing the collections needed to construct `BackupDocument`.
- `SqliteBackupDataStore(string connectionString)` initializes through `DatabaseInitializer`/`DatabaseSchema` and reads every user-data table with invariant timestamp and enum conversions.

- [x] **Step 1: Write a failing round-trip read test** that creates rows through the existing activity/history repositories, inserts representative exercise/template/plan/recommendation/settings rows, then asserts `ReadAsync` returns every value and relationship.
- [x] **Step 2: Run `dotnet test ... --filter FullyQualifiedName~SqliteBackupDataStoreTests`** and confirm the missing store/API failure.
- [x] **Step 3: Implement the read contract and parameter-free SELECT queries** ordered by stable keys; reuse the current schema and preserve nullable values, timestamps, decimals, status strings, and enum integers.
- [x] **Step 4: Re-run the focused store tests** and confirm complete data is read from a fresh SQLite file.
- [x] **Step 5: Commit** with `git add src/GymTracker.Core/Infrastructure/IBackupDataStore.cs src/GymTracker.Core/Infrastructure/SqliteBackupDataStore.cs tests/GymTracker.Tests/SqliteBackupDataStoreTests.cs` and `git commit -m "feat: read complete backup data set"`.

### Task 4: Implement transactional replace, merge, and local recovery copies

**Files:**
- Modify: `src/GymTracker.Core/Infrastructure/IBackupDataStore.cs`
- Modify: `src/GymTracker.Core/Infrastructure/SqliteBackupDataStore.cs`
- Test: `tests/GymTracker.Tests/SqliteBackupDataStoreTests.cs`

**Interfaces:**
- `ReplaceAsync(BackupDataSet, string recoveryCopyPath, CancellationToken)` clears and inserts all SQLite user-data tables in one transaction and returns `BackupMutationResult`.
- `MergeAsync(BackupDataSet, CancellationToken)` inserts only absent IDs, resolves references against existing/imported rows, and returns inserted/skipped counts plus validation errors when references cannot resolve.
- `CreateRecoveryCopyAsync(string destinationPath, CancellationToken)` copies the current database, including SQLite sidecars safely after checkpointing/closing connections.

- [x] **Step 1: Write failing tests** for replacement full round-trip, recovery-copy creation before replacement, rollback on an insert failure, merge insertion, merge ID conflict reporting, and unresolved merge references.
- [x] **Step 2: Run the focused store tests** and confirm the new mutation behavior fails.
- [x] **Step 3: Implement replacement with an explicit transaction**: checkpoint/close for the copy, copy the database, delete rows in child-to-parent order, insert in parent-to-child order, and commit only after all rows succeed.
- [x] **Step 4: Implement merge with existing-ID sets** for every table, foreign-key-safe ordering, conflict counts, and no deletes/overwrites.
- [x] **Step 5: Re-run the focused tests**, then run the existing SQLite repository tests to catch schema regressions.
- [x] **Step 6: Commit** with `git add src/GymTracker.Core/Infrastructure/IBackupDataStore.cs src/GymTracker.Core/Infrastructure/SqliteBackupDataStore.cs tests/GymTracker.Tests/SqliteBackupDataStoreTests.cs` and `git commit -m "feat: safely replace or merge backup data"`.

### Task 5: Orchestrate export/import files and active-workout state

**Files:**
- Create: `src/GymTracker.Core/Application/BackupService.cs`
- Test: `tests/GymTracker.Tests/BackupServiceTests.cs`

**Interfaces:**
- `BackupService(IBackupDataStore dataStore, IActiveWorkoutStore activeWorkoutStore, string appDataDirectory)`.
- `ExportAsync(CancellationToken)` returns `BackupExportResult` with path, file name, byte count, checksum, and document.
- `ValidateFileAsync(string, CancellationToken)` returns `BackupFileValidationResult` without mutating state.
- `ImportAsync(string, BackupImportMode, CancellationToken)` validates again, creates a timestamped recovery copy for replace, applies the selected SQLite mutation, and saves/clears the active snapshot according to the mode.

- [x] **Step 1: Write failing tests** for export contents, atomic export replacement, import rejection without mutation, replace backup path/result, merge result, active snapshot inclusion, and active snapshot replacement/retention rules.
- [x] **Step 2: Run `dotnet test ... --filter FullyQualifiedName~BackupServiceTests`** and confirm failure because the service is absent.
- [x] **Step 3: Implement service orchestration** using temporary JSON paths plus atomic `File.Move`, timestamped filenames, cancellation propagation, and clear result records; do not catch filesystem/database exceptions that the UI must report distinctly.
- [x] **Step 4: Re-run focused service tests** and confirm all import/export safety cases pass.
- [x] **Step 5: Commit** with `git add src/GymTracker.Core/Application/BackupService.cs tests/GymTracker.Tests/BackupServiceTests.cs` and `git commit -m "feat: orchestrate backup export and import"`.

### Task 6: Register the service and implement the Backup and Settings page

**Files:**
- Modify: `src/GymTracker.Core/Application/ServiceCollectionExtensions.cs`
- Modify: `src/GymTracker/MauiProgram.cs`
- Modify: `src/GymTracker/Pages/AppPages.cs`
- Test: `tests/GymTracker.Tests/ScaffoldTests.cs`

**Interfaces:**
- Registers one `IBackupDataStore` and one `BackupService` using the existing `gymtracker.db` app-data path and active-workout singleton.
- `BackupSettingsPage` invokes export/share and import/file-picker flows, displays validation messages, asks for explicit Replace/Merge confirmation, and reports mutation counts/recovery path.

- [x] **Step 1: Write failing scaffold/page tests** for service registration and removal of the placeholder “persistence is added” backup action text.
- [x] **Step 2: Run the focused scaffold tests** and confirm the placeholder behavior fails the new expectations.
- [x] **Step 3: Implement DI and thin MAUI handlers** using `FilePicker.Default.PickAsync`, `Share.Default.RequestAsync`, `DisplayAlert`, and page controls consistent with `FeaturePage`; keep all validation/mutation decisions in `BackupService`.
- [x] **Step 4: Re-run focused tests** and compile the MAUI project for the target desktop framework available in the environment.
- [x] **Step 5: Commit** with `git add src/GymTracker.Core/Application/ServiceCollectionExtensions.cs src/GymTracker/MauiProgram.cs src/GymTracker/Pages/AppPages.cs tests/GymTracker.Tests/ScaffoldTests.cs` and `git commit -m "feat: add backup settings flows"`.

### Task 7: Full verification and review handoff

**Files:**
- Modify: `docs/superpowers/plans/2026-09-02-issue-9-backup-import.md` to mark completed steps only as they are verified.

- [x] **Step 1: Run the complete test suite** with `dotnet test GymTracker.slnx --no-restore`; record the exit code and exact passed/failed counts.
- [x] **Step 2: Build the MAUI project** with the repository-supported target command and confirm exit code 0; if platform workloads prevent a build, record the exact workload error separately from test results.
- [x] **Step 3: Inspect `git diff master...HEAD`** for scope, secrets, unsafe file deletion, and accidental generated files.
- [x] **Step 4: Run `git status --short --branch`** and report the final branch, commits, tests, build result, and any remaining limitations.
