namespace GymTracker.Application;

public sealed record WorkoutSet(string Exercise, decimal Weight, int Reps)
{
    public decimal Volume => Weight * Reps;
}

public sealed class WorkoutSession
{
    private readonly List<WorkoutSet> sets = [];

    public WorkoutSession(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IReadOnlyList<WorkoutSet> Sets => sets;

    public int TotalSets => sets.Count;

    public decimal TotalVolume => sets.Sum(set => set.Volume);

    public void AddSet(string exercise, decimal weight, int reps)
    {
        if (string.IsNullOrWhiteSpace(exercise))
        {
            throw new ArgumentException("An exercise is required.", nameof(exercise));
        }

        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be greater than zero.");
        }

        if (reps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reps), "Reps must be greater than zero.");
        }

        sets.Add(new WorkoutSet(exercise.Trim(), weight, reps));
    }
}
