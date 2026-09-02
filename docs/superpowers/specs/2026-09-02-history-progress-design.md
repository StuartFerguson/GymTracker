# History and Progress Design

## Goal

Provide truthful history and progress browsing for persisted workout and activity records, replacing the current placeholder history and exercise-progress screens.

## Scope

- Browse completed workout sessions by date.
- Show exercise-specific progression from recorded sets.
- Surface personal bests, total sets, repetitions, training volume, and weekly consistency.
- Compare completed sets with planned sets when a session has a planned-session link.
- Browse activity history with distance and duration.

## Design

Add a read-only progress query service in `GymTracker.Core.Application`. It will consume the existing workout and activity persistence abstractions and return immutable summary records designed for presentation. Date-range filtering is inclusive and uses `DateOnly`; records are ordered newest-first for screens. Metrics use completed workout sets only, while planned-vs-completed comparison counts planned template items against completed sets for linked sessions.

The MAUI `HistoryPage` will load a selected date range and render session rows, summary metrics, planned/completed counts, and activity rows. `ExerciseProgressPage` will load exercise summaries and render best performance plus progression entries. Both pages will resolve the service through the existing MAUI service provider and show an explicit empty/error state. Navigation routes remain unchanged.

The existing domain records and SQLite schema remain compatible. New repository read methods will be added only where the current abstractions do not expose the persisted data required by the query service. No unrelated refactoring or future recommendation features are included.

## Testing

Unit tests will cover date ordering, completed-set metric aggregation, personal-best selection, weekly consistency, planned-versus-completed counts, and activity distance/duration history. Page changes will be verified through compilation and the existing test suite; data calculations remain isolated in the core project so they do not require a device UI test harness.
