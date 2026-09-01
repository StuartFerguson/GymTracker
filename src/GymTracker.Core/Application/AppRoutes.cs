namespace GymTracker.Application;

public static class AppRoutes
{
    public const string Dashboard = "Dashboard";
    public const string WeeklyPlan = "WeeklyPlan";
    public const string StartWorkout = "StartWorkout";
    public const string ActiveWorkout = "ActiveWorkout";
    public const string WorkoutSummary = "WorkoutSummary";
    public const string ActivityLog = "ActivityLog";
    public const string History = "History";
    public const string ExerciseProgress = "ExerciseProgress";
    public const string BackupSettings = "BackupSettings";

    public static IReadOnlyList<string> All { get; } =
    [
        Dashboard,
        WeeklyPlan,
        StartWorkout,
        ActiveWorkout,
        WorkoutSummary,
        ActivityLog,
        History,
        ExerciseProgress,
        BackupSettings
    ];
}
