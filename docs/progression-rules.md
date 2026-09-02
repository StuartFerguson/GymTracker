# Progression recommendation rules

`ProgressionRecommendationEngine` evaluates one exercise at a time. It uses
only the newest entry whose status is `Completed`, and takes the recorded
weight, repetitions, and notes as the source of truth, including values that
were edited after the workout.

Rules are evaluated in this order:

| Priority | Condition | Recommendation | Confidence | Rule key |
| --- | --- | --- | --- | --- |
| 1 | Latest completed notes contain `pain`, `injury`, or `hurt` | Repeat the latest successful value; do not increase | Low | `hold-pain` |
| 2 | Latest completed notes contain `very hard`, `too hard`, `max effort`, `RPE 9`, or `RPE 10` | Repeat the latest successful value; do not increase | Low | `hold-difficulty` |
| 3 | Exercise is marked recently unused and has successful history | Repeat the last successful value | Low | `fallback-last-success` |
| 4 | No successful history and caller supplies fallback repetitions (and weight for weighted exercises) | Use the supplied starting value | Low | `fallback-new-exercise` |
| 5 | Latest repetitions are below the target | Keep weight and add one repetition | Medium | `increase-repetitions` |
| 6 | Target repetitions are reached for a weighted exercise | Add the configured weight increment and reset to one repetition | Medium | `increase-weight` |
| 7 | Target repetitions are reached for a bodyweight exercise | Add one repetition and keep weight empty | Medium | `increase-repetitions` |

Failed, skipped, and incomplete entries never become progression inputs. If
there is no successful history and no complete fallback value, the engine
returns no recommendation. Every result includes the general-fitness safety
disclaimer; calculating a result does not mutate history or settings.
