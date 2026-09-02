namespace GymTracker.Application;

public enum ActivityType
{
    Walking,
    Running,
    Swimming
}

public sealed record ActivityEntry(
    Guid Id,
    DateOnly Date,
    ActivityType Type,
    int? DurationSeconds,
    decimal? DistanceMetres,
    int? Steps,
    string? Notes);

public sealed record ActivitySummary(
    int ActivityCount,
    int TotalDurationSeconds,
    decimal TotalDistanceMetres,
    IReadOnlyDictionary<ActivityType, int> Frequency)
{
    public int CountFor(ActivityType type) => Frequency.GetValueOrDefault(type);
}

public static class ActivityLogging
{
    public static ActivityEntry Create(
        DateOnly? date,
        ActivityType? type,
        int? durationSeconds = null,
        decimal? distanceMetres = null,
        int? steps = null,
        string? notes = null)
    {
        if (date is null)
        {
            throw new ArgumentException("A date is required.", nameof(date));
        }

        if (type is null || !Enum.IsDefined(type.Value))
        {
            throw new ArgumentException("A supported activity type is required.", nameof(type));
        }

        if (durationSeconds is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration cannot be negative.");
        }

        if (distanceMetres is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceMetres), "Distance cannot be negative.");
        }

        if (steps is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), "Steps cannot be negative.");
        }

        return new ActivityEntry(Guid.NewGuid(), date.Value, type.Value, durationSeconds, distanceMetres, steps, notes?.Trim());
    }

    public static TimeSpan? CalculatePace(ActivityEntry activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        if (activity.Type == ActivityType.Swimming || activity.DurationSeconds is not > 0 || activity.DistanceMetres is not > 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds((double)activity.DurationSeconds.Value / (double)activity.DistanceMetres.Value * 1000);
    }

    public static ActivitySummary GetWeeklySummary(IEnumerable<ActivityEntry> activities, DateOnly weekStart)
    {
        ArgumentNullException.ThrowIfNull(activities);
        var weekEnd = weekStart.AddDays(6);
        var matching = activities.Where(activity => activity.Date >= weekStart && activity.Date <= weekEnd).ToArray();
        var frequency = Enum.GetValues<ActivityType>().ToDictionary(type => type, _ => 0);
        foreach (var activity in matching)
        {
            frequency[activity.Type]++;
        }

        return new ActivitySummary(
            matching.Length,
            matching.Sum(activity => activity.DurationSeconds ?? 0),
            matching.Sum(activity => activity.DistanceMetres ?? 0),
            frequency);
    }
}
