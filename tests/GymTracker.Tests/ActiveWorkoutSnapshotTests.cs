using GymTracker.Application;

namespace GymTracker.Tests;

public sealed class ActiveWorkoutSnapshotTests
{
    [Fact]
    public void Snapshot_round_trip_preserves_all_active_set_values()
    {
        var original = new WorkoutSession("Push");
        original.AddSet("Dumbbell Bench Press", 22.5m, 8, "last hard set", WorkoutSetStatus.Incomplete, true);
        original.AddSet("Pull Up", null, 0, "missed", WorkoutSetStatus.Failed);

        var restored = WorkoutSession.FromSnapshot(original.ToSnapshot("Push"));

        Assert.Equal("Push", restored.Name);
        Assert.Equal(original.Sets, restored.Sets);
    }
}
