using GymTracker.Core.Domain;

namespace GymTracker.Core.Infrastructure;

public sealed record BuiltInSeed(
    IReadOnlyList<Exercise> Exercises,
    IReadOnlyList<ExerciseTemplate> Templates,
    IReadOnlyList<WeeklyPlanDay> WeeklyPlan);

public static class BuiltInSeedData
{
    private static readonly DateTimeOffset SeedTimestamp =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static BuiltInSeed Create()
    {
        var exercises = CreateExercises();
        var byName = exercises.ToDictionary(exercise => exercise.Name, StringComparer.Ordinal);

        var templates = new[]
        {
            CreateTemplate("Push", [
                Item(byName, "Barbell Bench Press", 4, 8, 60),
                Item(byName, "Incline Dumbbell Press", 3, 10, 22),
                Item(byName, "Overhead Press", 3, 8, 35),
                Item(byName, "Dumbbell Lateral Raise", 3, 12, 8),
                Item(byName, "Cable Triceps Pushdown", 3, 12, 25)]),
            CreateTemplate("Pull", [
                Item(byName, "Deadlift", 3, 5, 100),
                Item(byName, "Pull Up", 4, 8),
                Item(byName, "Barbell Row", 4, 8, 60),
                Item(byName, "Lat Pulldown", 3, 10, 50),
                Item(byName, "Face Pull", 3, 12, 20),
                Item(byName, "Dumbbell Curl", 3, 12, 12)]),
            CreateTemplate("Legs", [
                Item(byName, "Back Squat", 4, 8, 80),
                Item(byName, "Romanian Deadlift", 3, 10, 60),
                Item(byName, "Leg Press", 3, 10, 120),
                Item(byName, "Leg Curl", 3, 12, 35),
                Item(byName, "Standing Calf Raise", 4, 15, 50)]),
            CreateTemplate("Full Body", [
                Item(byName, "Back Squat", 3, 8, 70),
                Item(byName, "Barbell Bench Press", 3, 8, 50),
                Item(byName, "Barbell Row", 3, 8, 50),
                Item(byName, "Overhead Press", 3, 8, 30),
                Item(byName, "Pull Up", 3, 8),
                Item(byName, "Plank", 3, null, null)])
        };

        return new BuiltInSeed(
            Array.AsReadOnly(exercises),
            Array.AsReadOnly(templates),
            Array.AsReadOnly([
                new WeeklyPlanDay(DayOfWeek.Monday, "Push"),
                new WeeklyPlanDay(DayOfWeek.Tuesday, "Rest"),
                new WeeklyPlanDay(DayOfWeek.Wednesday, "Pull"),
                new WeeklyPlanDay(DayOfWeek.Thursday, "Rest"),
                new WeeklyPlanDay(DayOfWeek.Friday, "Legs"),
                new WeeklyPlanDay(DayOfWeek.Saturday, "Rest"),
                new WeeklyPlanDay(DayOfWeek.Sunday, "Full Body")]));
    }

    private static Exercise[] CreateExercises() =>
    [
        Exercise("Barbell Bench Press", ExerciseCategory.Chest),
        Exercise("Incline Dumbbell Press", ExerciseCategory.Chest),
        Exercise("Deadlift", ExerciseCategory.Back),
        Exercise("Barbell Row", ExerciseCategory.Back),
        Exercise("Lat Pulldown", ExerciseCategory.Back),
        Exercise("Pull Up", ExerciseCategory.Back, ExerciseType.Repetition, MeasurementUnit.Repetitions),
        Exercise("Overhead Press", ExerciseCategory.Shoulders),
        Exercise("Dumbbell Lateral Raise", ExerciseCategory.Shoulders),
        Exercise("Face Pull", ExerciseCategory.Shoulders),
        Exercise("Cable Triceps Pushdown", ExerciseCategory.Arms),
        Exercise("Dumbbell Curl", ExerciseCategory.Arms),
        Exercise("Back Squat", ExerciseCategory.Legs),
        Exercise("Romanian Deadlift", ExerciseCategory.Legs),
        Exercise("Leg Press", ExerciseCategory.Legs),
        Exercise("Leg Curl", ExerciseCategory.Legs),
        Exercise("Standing Calf Raise", ExerciseCategory.Legs),
        Exercise("Plank", ExerciseCategory.Core, ExerciseType.Duration, MeasurementUnit.Seconds)
    ];

    private static Exercise Exercise(
        string name,
        ExerciseCategory category,
        ExerciseType type = ExerciseType.Weight,
        MeasurementUnit unit = MeasurementUnit.Kilograms) =>
        new(StableId(name), name, type, unit, true) { Category = category };

    private static ExerciseTemplate CreateTemplate(string name, ExerciseTemplateItem[] items) =>
        new(StableId($"template:{name}"), name,
            Array.AsReadOnly(items.Select((item, position) => item with { Position = position }).ToArray()),
            SeedTimestamp);

    private static ExerciseTemplateItem Item(
        IReadOnlyDictionary<string, Exercise> exercises,
        string name,
        int sets,
        int? repetitions,
        decimal? weight = null) =>
        new(exercises[name].Id, name, 0, sets, repetitions, weight);

    private static Guid StableId(string key) =>
        GuidUtility.Create(GuidUtility.Namespace, $"gym-tracker:v1:{key}");

    private static class GuidUtility
    {
        public static readonly Guid Namespace = new("4f8c2f5f-6b50-4dbe-ae06-6dc8ce0b0f9f");

        public static Guid Create(Guid namespaceId, string name)
        {
            var namespaceBytes = namespaceId.ToByteArray();
            var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
            var bytes = new byte[namespaceBytes.Length + nameBytes.Length];
            namespaceBytes.CopyTo(bytes, 0);
            nameBytes.CopyTo(bytes, namespaceBytes.Length);
            var hash = System.Security.Cryptography.SHA1.HashData(bytes);
            hash[6] = (byte)((hash[6] & 0x0f) | 0x50);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            return new Guid(hash[..16]);
        }
    }
}
