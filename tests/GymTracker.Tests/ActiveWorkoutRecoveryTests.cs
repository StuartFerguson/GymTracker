using GymTracker.Application;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class ActiveWorkoutRecoveryTests
{
    [Fact]
    public async Task Recovery_coordinator_restores_the_saved_session()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gymtracker-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonActiveWorkoutStore(path);
            var session = new WorkoutSession("Legs");
            session.AddSet("Back Squat", 80, 6, "felt strong");
            var coordinator = new ActiveWorkoutRecovery(store);

            await coordinator.SaveAsync(session, "Legs");
            var recovery = await coordinator.LoadAsync();

            Assert.NotNull(recovery);
            Assert.Equal(session.Sets, recovery!.Session.Sets);
            Assert.Equal("Legs", recovery.TemplateName);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
