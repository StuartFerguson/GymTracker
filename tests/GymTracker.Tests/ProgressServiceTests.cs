using GymTracker.Application;
using GymTracker.Core.Domain;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class ProgressServiceTests
{
    [Fact]
    public async Task History_aggregates_completed_metrics_and_activity_history()
    {
        var first = new WorkoutSessionRecord(Guid.NewGuid(), null, "Push", At(2026, 9, 1), At(2026, 9, 2), "kg");
        var second = new WorkoutSessionRecord(Guid.NewGuid(), null, "Pull", At(2026, 9, 8), At(2026, 9, 9), "kg");
        var firstSets = new[]
        {
            Set(first, 1, 60, 8, "Completed"), Set(first, 2, 70, 5, "Skipped")
        };
        var secondSets = new[] { Set(second, 1, 80, 5, "Completed") };
        var service = new ProgressService(
            new FakeWorkoutRepository([
                new WorkoutHistoryEntry(first, firstSets, 3),
                new WorkoutHistoryEntry(second, secondSets)]),
            new FakeActivityRepository([new ActivityEntry(Guid.NewGuid(), new DateOnly(2026, 9, 8), ActivityType.Running, 1800, 5000, null, null)]));

        var report = await service.GetHistoryAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14));

        Assert.Equal(2, report.Metrics.WorkoutCount);
        Assert.Equal(2, report.Metrics.TotalSets);
        Assert.Equal(13, report.Metrics.TotalRepetitions);
        Assert.Equal(880, report.Metrics.TrainingVolumeKg);
        Assert.Equal(2, report.Metrics.ConsistentWeeks);
        Assert.Equal(3, report.Workouts[0].PlannedSetCount);
        Assert.Equal(1, report.Workouts[0].CompletedSetCount);
        Assert.Equal(5000, report.Activities[0].DistanceMetres);
        Assert.Equal(1800, report.Activities[0].DurationSeconds);
    }

    [Fact]
    public async Task Exercise_progress_selects_weight_then_repetitions_as_the_personal_best()
    {
        var session = new WorkoutSessionRecord(Guid.NewGuid(), null, "Push", At(2026, 9, 1), At(2026, 9, 2), "kg");
        var service = new ProgressService(
            new FakeWorkoutRepository([new WorkoutHistoryEntry(session, [Set(session, 1, 60, 8, "Completed"), Set(session, 2, 60, 10, "Completed"), Set(session, 3, 100, 1, "Incomplete")])]),
            new FakeActivityRepository([]));

        var result = Assert.Single(await service.GetExerciseProgressAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1)));

        Assert.Equal(60, result.BestWeightKg);
        Assert.Equal(10, result.BestRepetitions);
        Assert.Equal(2, result.Entries.Count);
    }

    private static WorkoutSetRecord Set(WorkoutSessionRecord session, int number, decimal weight, int reps, string status) =>
        new(Guid.NewGuid(), session.Id, Guid.NewGuid(), "Bench Press", number, weight, reps, "kg", null, status);

    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 18, 0, 0, TimeSpan.Zero);

    private sealed class FakeWorkoutRepository(IReadOnlyList<WorkoutHistoryEntry> entries) : IWorkoutHistoryRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(WorkoutSessionRecord session, IReadOnlyList<WorkoutSetRecord> sets, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkoutHistoryEntry>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => Task.FromResult(entries);
    }

    private sealed class FakeActivityRepository(IReadOnlyList<ActivityEntry> entries) : IActivityRepository
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddAsync(ActivityEntry activity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ActivityEntry>> ListAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default) => Task.FromResult(entries);
    }
}
