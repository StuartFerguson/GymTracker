namespace GymTracker.Application;

public enum WorkoutSetStatus
{
    Completed,
    Failed,
    Skipped,
    Incomplete
}

public sealed record WorkoutSet(
    string Exercise,
    decimal? Weight,
    int Reps,
    string? Notes = null,
    WorkoutSetStatus Status = WorkoutSetStatus.Completed,
    bool IsPerDumbbell = false)
{
    public decimal Volume => (Weight ?? 0) * Reps;
}

public sealed class WorkoutSession
{
    private readonly List<WorkoutSet> sets = [];
    private readonly Dictionary<string, WorkoutSet> previousSets;

    public WorkoutSession(string name, IEnumerable<WorkoutSet>? previousSessionSets = null)
    {
        Name = name;
        previousSets = (previousSessionSets ?? [])
            .GroupBy(set => set.Exercise, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    public IReadOnlyList<WorkoutSet> Sets => sets;

    public int TotalSets => sets.Count;

    public decimal TotalVolume => sets.Sum(set => set.Volume);

    public void AddSet(
        string exercise,
        decimal? weight,
        int reps,
        string? notes = null,
        WorkoutSetStatus status = WorkoutSetStatus.Completed,
        bool isPerDumbbell = false)
    {
        sets.Add(CreateSet(exercise, weight, reps, notes, status, isPerDumbbell));
    }

    public void UpdateSet(
        int index,
        decimal? weight,
        int reps,
        string? notes = null,
        WorkoutSetStatus status = WorkoutSetStatus.Completed,
        bool isPerDumbbell = false)
    {
        if (index < 0 || index >= sets.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The set does not exist.");
        }

        var existing = sets[index];
        sets[index] = CreateSet(existing.Exercise, weight, reps, notes, status, isPerDumbbell);
    }

    public WorkoutSet? GetPreviousSet(string exercise)
    {
        if (string.IsNullOrWhiteSpace(exercise))
        {
            return null;
        }

        return previousSets.GetValueOrDefault(exercise.Trim());
    }

    private static WorkoutSet CreateSet(
        string exercise,
        decimal? weight,
        int reps,
        string? notes,
        WorkoutSetStatus status,
        bool isPerDumbbell)
    {
        if (string.IsNullOrWhiteSpace(exercise))
        {
            throw new ArgumentException("An exercise is required.", nameof(exercise));
        }

        if (weight is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be greater than zero when supplied.");
        }

        if (reps < 0 || (status == WorkoutSetStatus.Completed && reps == 0))
        {
            throw new ArgumentOutOfRangeException(nameof(reps), "Reps must be zero or greater, and completed sets need reps.");
        }

        return new WorkoutSet(exercise.Trim(), weight, reps, notes?.Trim(), status, isPerDumbbell);
    }
}
