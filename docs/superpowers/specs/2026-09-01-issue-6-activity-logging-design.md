# Activity logging design

## Goal

Allow users to manually record walking, running, and swimming activities
without coupling those records to gym workout sessions.

## Scope

The feature includes a validated activity model, local SQLite persistence,
manual-entry UI, pace calculation for walking and running, and weekly totals
and frequency summaries. It does not import from device health platforms or
automatically record GPS activity.

## Design

`ActivityRecord` remains a separate domain record. An application service will
validate user input and calculate derived pace without storing pace as a
second source of truth. Walking, running, and swimming are the only accepted
activity types for this release. Date and type are required; duration,
distance, steps, and notes are optional. Pace is available only when duration
and distance are both present and the activity is walking or running.

The infrastructure layer will expose an asynchronous activity repository over
the existing `activities` SQLite table. Inserts use parameters, timestamps are
stored in UTC, and summary queries aggregate records by a supplied local-week
date range. The UI will call the application service and repository through
dependency injection; it will not contain SQL or calculation rules.

## Safety and error handling

- Invalid type, date, negative duration/distance/steps, or zero-distance pace
  input is rejected with a user-safe validation message.
- Storage failures leave the form data intact and show a recoverable error.
- No network, account, location, or health-platform permission is introduced.
- Activity copy describes recorded metrics only and does not provide medical
  advice.

## Testing

Tests cover validation, pace calculation, weekly aggregation, frequency
counts, persistence round trips, and invalid input. Repository tests use a
temporary SQLite database and the real SQL implementation.
