using GymTracker.Application;

namespace GymTracker.Tests;

public sealed class BuiltInWorkoutCatalogTests
{
    [Fact]
    public void Starting_a_template_creates_an_empty_session_with_the_template_name()
    {
        var catalog = new BuiltInWorkoutCatalog();

        var session = catalog.StartSession("Push");

        Assert.Equal("Push", session.Name);
        Assert.Empty(session.Sets);
    }

    [Fact]
    public void Template_exercises_are_available_in_their_defined_order()
    {
        var catalog = new BuiltInWorkoutCatalog();

        var exercises = catalog.GetTemplate("Push").Items.Select(item => item.ExerciseNameSnapshot);

        Assert.Equal(
            [
                "Barbell Bench Press",
                "Incline Dumbbell Press",
                "Overhead Press",
                "Dumbbell Lateral Raise",
                "Cable Triceps Pushdown"
            ],
            exercises);
    }
}
