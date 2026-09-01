using GymTracker.Core.Domain;
using GymTracker.Core.Infrastructure;

namespace GymTracker.Application;

public sealed class BuiltInWorkoutCatalog
{
    private readonly BuiltInSeed seed = BuiltInSeedData.Create();

    public IReadOnlyList<Exercise> Exercises => seed.Exercises;

    public IReadOnlyList<ExerciseTemplate> Templates => seed.Templates;

    public IReadOnlyList<WeeklyPlanDay> WeeklyPlan => seed.WeeklyPlan;

    public ExerciseTemplate GetTemplate(string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        return Templates.Single(template =>
            string.Equals(template.Name, templateName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public WorkoutSession StartSession(string templateName)
    {
        var template = GetTemplate(templateName);
        return new WorkoutSession(template.Name);
    }
}
