namespace GymTracker.Application;

public sealed record ActiveWorkoutSnapshot(
    string SessionName,
    string? TemplateName,
    IReadOnlyList<WorkoutSetSnapshot> Sets);

public sealed record WorkoutSetSnapshot(
    string Exercise,
    decimal? Weight,
    int Reps,
    string? Notes,
    WorkoutSetStatus Status,
    bool IsPerDumbbell);
