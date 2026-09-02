# Issue #7 Progression Recommendation Engine Design

## Goal

Provide deterministic, explainable training guidance that suggests the next weight and repetition target for a recorded exercise without making automatic changes to the user's workout plan.

## Scope

- Calculate a recommendation from completed workout history and the current exercise target.
- Use a repetition-first progression rule, then increase weight by a fixed configurable increment and reset repetitions.
- Block automatic increases when recent notes indicate pain or high difficulty.
- Fall back to the last successful value for new or recently unused exercises with low confidence.
- Return a recommendation that callers can accept, edit, or ignore.
- Keep the engine platform-independent and independently unit-testable.

The engine does not persist recommendations, mutate workout history, or add UI controls in this issue. Existing persistence and backup support for the `Recommendation` record remain compatible.

## Design

Add a focused `ProgressionRecommendationEngine` in `GymTracker.Core.Application`. It accepts an exercise identifier and name, target repetitions, weight increment, and completed history entries. The input is an immutable request; each history entry contains the recorded weight, repetitions, and optional notes, plus a timestamp for deterministic recent-history evaluation.

The engine evaluates only completed entries for the requested exercise. It orders entries newest-first, identifies the latest successful value, and returns an immutable result with proposed weight/repetitions, confidence, rule key, human-readable explanation, and the safety disclaimer that the result is general fitness guidance rather than medical advice.

The default rule is:

1. If there is no successful history, return no recommendation.
2. If recent notes contain pain indicators, do not increase; repeat the latest successful value with low confidence and a pain warning.
3. If recent notes contain high-difficulty indicators, do not increase; repeat the latest successful value with low confidence and a difficulty warning.
4. If the latest successful repetitions are below the target, retain weight and add one repetition, capped at the target.
5. Once the target repetitions are achieved, increase weight by the configured increment and reset repetitions to one.

The engine treats notes case-insensitively and uses a documented finite vocabulary for pain and difficulty indicators. A missing weight is valid for bodyweight/repetition exercises; such exercises progress repetitions only. Invalid requests, non-positive targets, or non-positive weight increments for weighted exercises are rejected with argument exceptions. The caller owns acceptance, editing, and ignoring; calculating a result has no side effects.

## Testing

Unit tests will cover:

- repetition increases below target;
- weight increase at target with repetition reset;
- no increase for pain notes;
- no increase for high-difficulty notes;
- low-confidence fallback for new or recently unused exercises;
- bodyweight/repetition-only progression;
- completed-only filtering and deterministic ordering;
- explanation, rule key, confidence, and safety disclaimer contents;
- invalid input rejection and no-history behavior.

No device UI test harness or network dependency is required.
