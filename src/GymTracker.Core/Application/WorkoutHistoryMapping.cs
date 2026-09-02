using GymTracker.Core.Domain;

namespace GymTracker.Application;

public sealed record WorkoutHistoryRecords(
    WorkoutSessionRecord Session,
    IReadOnlyList<WorkoutSetRecord> Sets);

public static class WorkoutHistoryMapping
{
    public static WorkoutHistoryRecords ToRecords(
        WorkoutSession session,
        IReadOnlyList<Exercise> exercises,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(exercises);
        if (completedAt < startedAt) throw new ArgumentException("Completion cannot precede start.", nameof(completedAt));

        var sessionId = Guid.NewGuid();
        var exerciseIds = exercises.ToDictionary(exercise => exercise.Name, exercise => exercise.Id, StringComparer.OrdinalIgnoreCase);
        var sets = session.Sets.Select((set, index) =>
        {
            if (!exerciseIds.TryGetValue(set.Exercise, out var exerciseId))
            {
                exerciseId = Guid.Empty;
            }

            return new WorkoutSetRecord(Guid.NewGuid(), sessionId, exerciseId, set.Exercise, index + 1,
                set.Weight, set.Reps, "kg", set.Notes, set.Status.ToString());
        }).ToArray();

        return new WorkoutHistoryRecords(
            new WorkoutSessionRecord(sessionId, null, session.Name, startedAt, completedAt, "kg"),
            sets);
    }
}
