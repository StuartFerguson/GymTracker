using GymTracker.Core.Domain;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Tests;

public sealed class SeedDataTests
{
    [Fact]
    public void Built_in_seed_is_reproducible()
    {
        var first = BuiltInSeedData.Create();
        var second = BuiltInSeedData.Create();

        Assert.Equal(first.Exercises.Select(exercise => exercise with { }), second.Exercises);
        Assert.Equal(
            first.Templates.Select(template => (template.Name, Items: template.Items.ToArray())),
            second.Templates.Select(template => (template.Name, Items: template.Items.ToArray())));
        Assert.Equal(first.WeeklyPlan, second.WeeklyPlan);
    }

    [Fact]
    public void Built_in_catalogue_contains_the_required_categories_and_exercises()
    {
        var seed = BuiltInSeedData.Create();

        Assert.Contains(seed.Exercises, exercise => exercise.Category == ExerciseCategory.Chest);
        Assert.Contains(seed.Exercises, exercise => exercise.Category == ExerciseCategory.Back);
        Assert.Contains(seed.Exercises, exercise => exercise.Category == ExerciseCategory.Shoulders);
        Assert.Contains(seed.Exercises, exercise => exercise.Category == ExerciseCategory.Arms);
        Assert.Contains(seed.Exercises, exercise => exercise.Category == ExerciseCategory.Legs);
        Assert.Contains(seed.Exercises, exercise => exercise.Category == ExerciseCategory.Core);
        Assert.Contains(seed.Exercises, exercise => exercise.Name == "Barbell Bench Press");
        Assert.Contains(seed.Exercises, exercise => exercise.Name == "Barbell Row");
        Assert.Contains(seed.Exercises, exercise => exercise.Name == "Back Squat");
        Assert.Contains(seed.Exercises, exercise => exercise.Name == "Pull Up");
    }

    [Fact]
    public void Built_in_templates_cover_the_four_training_splits()
    {
        var seed = BuiltInSeedData.Create();

        Assert.Equal(["Push", "Pull", "Legs", "Full Body"], seed.Templates.Select(template => template.Name));
        Assert.All(seed.Templates, template => Assert.NotEmpty(template.Items));
        Assert.All(seed.Templates.SelectMany(template => template.Items), item =>
            Assert.Equal(item.ExerciseNameSnapshot, seed.Exercises.Single(exercise => exercise.Id == item.ExerciseId).Name));
    }

    [Fact]
    public void Built_in_weekly_plan_has_one_entry_for_each_day()
    {
        var seed = BuiltInSeedData.Create();

        Assert.Equal(7, seed.WeeklyPlan.Count);
        Assert.Equal(
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday],
            seed.WeeklyPlan.Select(day => day.Day));
        Assert.Equal(["Push", "Rest", "Pull", "Rest", "Legs", "Rest", "Full Body"],
            seed.WeeklyPlan.Select(day => day.TemplateName));
    }

    [Fact]
    public void Template_items_keep_name_snapshots_when_catalogue_records_change()
    {
        var seed = BuiltInSeedData.Create();
        var pushItem = seed.Templates.Single(template => template.Name == "Push").Items[0];
        var renamedExercise = seed.Exercises.Single(exercise => exercise.Id == pushItem.ExerciseId) with { Name = "Renamed" };

        Assert.Equal("Barbell Bench Press", pushItem.ExerciseNameSnapshot);
        Assert.NotEqual(renamedExercise.Name, pushItem.ExerciseNameSnapshot);
    }
}
