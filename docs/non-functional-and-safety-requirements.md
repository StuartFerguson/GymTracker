# Non-functional and safety requirements

These requirements apply to the first usable offline release of GymTracker.
They are cross-cutting constraints: feature work must preserve them even when
the feature is not itself a persistence, privacy, or recommendation feature.

## Requirements

| ID | Requirement | Acceptance condition |
| --- | --- | --- |
| NFR-001 | The app shall work fully offline. | Core workout, history, progress, and settings flows do not require network access, an account, or a remote service. |
| NFR-002 | An active workout shall survive an unexpected app close. | After the app is relaunched, the user can resume the latest successfully saved active workout, including its sets, notes, statuses, and weights. |
| NFR-003 | User data shall not be lost during edits or imports. | A failed or interrupted write leaves the last valid data intact; imports are validated before replacing existing data and do not partially apply. |
| NFR-004 | The app shall collect only data necessary for the stated workout features. | No account, advertising identifier, location, contacts, health-service integration, telemetry, or unnecessary personal profile is introduced without a separately reviewed requirement and user consent. |
| SAF-001 | Recommendations shall be presented as training guidance, not medical advice. | Recommendation UI and copy clearly state that guidance is general fitness information and does not diagnose, treat, or replace advice from a qualified health professional. |

## Current implementation evidence

The following repository behavior already supports these requirements:

- The app has no backend, account flow, network client, or external health
  integration in the current composition root.
- Active workouts are stored in the platform app-data directory through an
  injected local store. Saves are written to a temporary file, flushed, and
  atomically moved over the previous snapshot.
- Invalid active-workout snapshots are discarded without preventing a fresh
  workout from starting. A failed replacement cannot overwrite the last valid
  snapshot.
- `WorkoutSession` validates imported/recovered set values through the same
  path used for new set entries.

These behaviors are covered by the core and recovery tests, including snapshot
round trips, malformed files, failed replacements, clear operations, and
recovery of an active session.

## Rules for future work

1. New core workout functionality must remain usable without network access.
2. Persistence changes must use atomic replacement or a transaction boundary;
   a partially written import must never become the current dataset.
3. User-entered workout data must stay local unless an explicitly approved
   feature changes that boundary and documents consent, export, and deletion
   behavior.
4. Historical records must retain the names and values needed to explain what
   the user actually logged; later catalogue edits must not rewrite history.
5. Recommendation features must use non-prescriptive language and include the
   training-guidance disclaimer at the point where the recommendation is
   shown.

## Verification expectations

Every feature that touches one of these constraints should add focused tests
for its failure path. At minimum:

- offline flows must be testable without a network or service dependency;
- persistence and imports must test interruption/validation failure without
  losing the previous valid data;
- recovery must test unexpected-close behavior and all persisted fields;
- recommendation tests must verify the displayed guidance and disclaimer;
- privacy-sensitive changes must document the exact data collected and why it
  is necessary.
