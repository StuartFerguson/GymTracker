# Versioned JSON Backup And Import Design

**Issue:** #9 — Implement versioned JSON backup and import with validation

## Goal

Allow users to export all locally persisted GymTracker state to a portable,
versioned JSON file and restore it safely after validation, with an automatic
recoverable local backup before replacement imports.

## Scope

The backup contains the app's persisted SQLite state and the active-workout
recovery snapshot:

- exercises;
- exercise templates and template items;
- planned sessions;
- completed workout sessions and sets;
- logged activities;
- recommendations;
- user settings;
- the active-workout snapshot, when present.

`backup_metadata` is operational metadata and is not imported as user data.
The export includes a format version and creation timestamp. The current
format is `1`; unsupported future versions are rejected without touching local
data.

## Architecture

Add a platform-neutral backup application service and a SQLite boundary. The
service owns the JSON contract, serialization, structural validation, checksum
calculation, and orchestration of export/import. The SQLite boundary owns
reading the complete persisted data set and applying a validated replacement or
merge transaction. Existing repositories remain responsible for their current
feature flows.

The MAUI layer owns user-facing file selection and sharing. `BackupSettingsPage`
resolves the service through dependency injection, asks for explicit
confirmation before replacement or merge, and renders success, validation, and
I/O failures. The core service remains usable from tests without MAUI controls.

## Backup contract

Use immutable records in `GymTracker.Core.Application`:

```csharp
public sealed record BackupDocument(
    string FormatVersion,
    DateTimeOffset ExportedAt,
    string Checksum,
    IReadOnlyList<Exercise> Exercises,
    IReadOnlyList<ExerciseTemplate> ExerciseTemplates,
    IReadOnlyList<PlannedSession> PlannedSessions,
    IReadOnlyList<WorkoutSessionRecord> WorkoutSessions,
    IReadOnlyList<WorkoutSetRecord> WorkoutSets,
    IReadOnlyList<ActivityRecord> Activities,
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<UserSettings> UserSettings,
    ActiveWorkoutSnapshot? ActiveWorkout);
```

The JSON uses camelCase property names and enum strings, with stable ordering
of each collection by its natural key or timestamp. The serialized UTF-8
payload is checksummed with SHA-256. Export calculates the checksum from a
canonical document with `Checksum` blank, then writes the resulting value into
the document; import blanks the field and repeats the calculation before any
mutation.

## Export flow

1. Read all SQLite rows and the active-workout snapshot.
2. Construct a `BackupDocument` with format version `1`.
3. Serialize using the shared JSON options and calculate its SHA-256 checksum.
4. Write the JSON atomically to a timestamped file in app storage.
5. Return an export result containing the path, file name, byte count, and
   checksum so the MAUI page can show status and offer sharing.

An export failure leaves the previous export untouched and does not alter the
database.

## Import validation and mutation

The service parses the complete JSON before opening a write transaction. It
rejects malformed JSON, missing required top-level collections, unsupported
format versions, invalid enum values, blank required strings, duplicate IDs,
duplicate template-item positions, duplicate set numbers within a session,
orphaned references, invalid date/time values, and negative numeric values
where the domain disallows them. Validation returns all discovered messages in
one result so the user can correct the file in a single iteration.

The user chooses one of two modes after validation:

- **Replace:** create a timestamped copy of the current SQLite database,
  clear imported user-data tables, insert the document in foreign-key-safe
  order, and replace the active-workout snapshot. The database operation is a
  single transaction; on failure it rolls back and the local copy remains
  recoverable.
- **Merge:** retain existing rows, insert imported rows whose IDs are absent,
  and report skipped ID conflicts. References must resolve against either the
  existing database or the imported document. The active-workout snapshot is
  replaced only when the imported document contains one and the user confirms
  the merge.

The backup copy is created before replacement mutation. Import never deletes
or overwrites local user data before validation succeeds and confirmation is
complete.

## MAUI interaction

`BackupSettingsPage` will provide:

- current backup status;
- Create backup, followed by platform sharing of the generated JSON file;
- Import backup, file selection, validation summary, and a Replace/Merge
  choice;
- a confirmation warning that names the selected mutation mode;
- success feedback including inserted/skipped counts and the local recovery
  backup path for replacement imports;
- clear error feedback for invalid files and filesystem/database failures.

The implementation will use the existing Shell/page style and MAUI platform
APIs already available in the project, without introducing an account,
network, or cloud-backup dependency.

## Testing strategy

Unit tests will cover:

- stable versioned JSON round trips and checksum verification;
- aggregate validation of malformed, incomplete, duplicate, invalid-reference,
  and invalid-value documents;
- export file creation and failure atomicity;
- replacement backup creation, transactional rollback, and full round trip;
- merge insertion and conflict reporting;
- active-workout snapshot inclusion and replacement behavior.

The existing full xUnit suite and MAUI project compilation will be run after
implementation. UI behavior will be kept thin and verified through compilation
plus core/service tests.
