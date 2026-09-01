# Active Workout Recovery Design

## Goal

Persist the current workout locally after every meaningful change so an unexpected app close does not lose in-progress work, while completing or cancelling a workout removes the recoverable snapshot.

## Scope

This change covers only the active workout snapshot. Completed workout history and the existing SQLite schema remain out of scope because the repository does not yet contain a concrete SQLite data-access implementation.

## Design

The core application will expose an `IActiveWorkoutStore` with asynchronous `SaveAsync`, `LoadAsync`, and `ClearAsync` operations. The store will persist a serializable `ActiveWorkoutSnapshot` containing the workout name, selected template name, and every logged set including weight, reps, notes, status, and per-dumbbell metadata.

The default file implementation will write beneath `FileSystem.AppDataDirectory` using a fixed filename. Each save writes a complete JSON document to a sibling temporary file, flushes and closes it, then replaces the previous snapshot. A failed or interrupted write therefore leaves either the last complete snapshot or no new snapshot; a partial JSON document is never treated as valid recovery state. Invalid or unreadable snapshots are ignored and removed so the user can continue with a fresh workout.

`WorkoutSession` will gain a snapshot conversion path that reconstructs its existing validation and behavior without treating recovered sets as new edits. The app will create the store through dependency injection and the active-workout page will load recovery state when it is created. Set add and edit operations will save after a successful mutation. Finishing or cancelling will clear the snapshot after the navigation action is accepted.

Recovery is explicit in the UI: when an active snapshot exists, the start-workout flow offers to resume it; otherwise it behaves normally. A declined recovery clears only the active snapshot and does not affect completed records. Quick-start and template sessions both use the same snapshot format.

## Error handling

- Save failures are surfaced through the page feedback label and do not discard the in-memory session.
- Load failures are treated as recoverable storage errors: the bad snapshot is removed and a new session can be started.
- Clear failures are reported but do not prevent the completed/cancelled navigation from occurring.
- Cancellation tokens are passed through every store operation.

## Testing

Tests will use an injected temporary path and the real JSON store. They will verify:

- Full snapshot round-trip, including set edits, failed/skipped/incomplete states, notes, and per-dumbbell values.
- A missing snapshot returns no recovery state.
- A malformed snapshot is ignored and removed.
- A failed replacement cannot overwrite the last valid snapshot.
- Clear removes the recoverable state.
- App-facing session recovery restores the same session name, template, and sets.

## Non-goals

- Persisting completed workout history.
- Adding a SQLite provider or migrations.
- Cloud backup, accounts, or cross-device synchronization.
