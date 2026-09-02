using System.Globalization;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Application;

public sealed class ProgressService(
    IWorkoutHistoryRepository workoutHistoryRepository,
    IActivityRepository activityRepository)
{
    public async Task<HistoryReport> GetHistoryAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var history = await workoutHistoryRepository.ListAsync(from, to, cancellationToken);
        var activities = await activityRepository.ListAsync(from, to, cancellationToken);
        var completedSets = history.SelectMany(item => item.Sets).Where(IsCompleted).ToArray();
        var weeks = history.Where(item => completedSets.Any(set => set.WorkoutSessionId == item.Session.Id))
            .Select(item => ISOWeek.GetWeekOfYear(item.Session.StartedAtUtc.UtcDateTime.Date))
            .Distinct().Count();

        var workouts = history.Select(item => new WorkoutHistorySummary(
            item.Session.StartedAt, item.Session.TemplateNameSnapshot,
            item.Sets.Count(IsCompleted), item.PlannedSetCount,
            item.Sets.Where(IsCompleted).Sum(set => set.Repetitions ?? 0),
            item.Sets.Where(IsCompleted).Sum(Volume))).ToArray();

        return new HistoryReport(workouts,
            new ProgressMetrics(workouts.Length, completedSets.Length,
                completedSets.Sum(set => set.Repetitions ?? 0), completedSets.Sum(Volume), weeks),
            activities.OrderByDescending(activity => activity.Date)
                .Select(activity => new ActivityHistorySummary(activity.Date, activity.Type, activity.DurationSeconds ?? 0, activity.DistanceMetres ?? 0)).ToArray());
    }

    public async Task<IReadOnlyList<ExerciseProgressSummary>> GetExerciseProgressAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var history = await workoutHistoryRepository.ListAsync(from, to, cancellationToken);
        return history.SelectMany(item => item.Sets.Where(IsCompleted).Select(set => (item.Session.StartedAt, Set: set)))
            .GroupBy(item => item.Set.ExerciseNameSnapshot, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var entries = group.OrderBy(item => item.StartedAt).Select(item => new ExerciseProgressEntry(item.StartedAt, item.Set.WeightKg, item.Set.Repetitions ?? 0)).ToArray();
                var best = entries.OrderByDescending(entry => entry.WeightKg ?? 0).ThenByDescending(entry => entry.Repetitions).First();
                return new ExerciseProgressSummary(group.Key, best.WeightKg, best.Repetitions, entries);
            }).ToArray();
    }

    private static bool IsCompleted(Core.Domain.WorkoutSetRecord set) => string.Equals(set.Status, nameof(WorkoutSetStatus.Completed), StringComparison.OrdinalIgnoreCase);
    private static decimal Volume(Core.Domain.WorkoutSetRecord set) => (set.WeightKg ?? 0) * (set.Repetitions ?? 0);
}
