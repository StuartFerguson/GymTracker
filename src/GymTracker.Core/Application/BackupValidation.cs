using GymTracker.Core.Domain;

namespace GymTracker.Application;

public static class BackupValidation
{
    public static BackupValidationResult Validate(BackupDocument? document)
    {
        var errors = new List<string>();
        if (document is null)
        {
            errors.Add("Document is required.");
            return new BackupValidationResult(errors);
        }

        if (!string.Equals(document.FormatVersion, "1", StringComparison.Ordinal))
        {
            errors.Add("FormatVersion must be '1'.");
        }

        if (document.ExportedAt == default)
        {
            errors.Add("ExportedAt must be a valid timestamp.");
        }

        ValidateExercises(document.Exercises, errors);
        ValidateTemplates(document.ExerciseTemplates, document.Exercises, errors);
        ValidatePlannedSessions(document.PlannedSessions, document.ExerciseTemplates, errors);
        ValidateWorkoutSessions(document.WorkoutSessions, document.PlannedSessions, errors);
        ValidateWorkoutSets(document.WorkoutSets, document.WorkoutSessions, document.Exercises, errors);
        ValidateActivities(document.Activities, errors);
        ValidateRecommendations(document.Recommendations, document.Exercises, errors);
        ValidateSettings(document.UserSettings, errors);
        ValidateActiveWorkout(document.ActiveWorkout, errors);

        return new BackupValidationResult(errors);
    }

    private static void ValidateExercises(IReadOnlyList<Exercise>? exercises, List<string> errors)
    {
        if (exercises is null)
        {
            errors.Add("Exercises is required.");
            return;
        }

        AddDuplicateIdErrors(exercises.Select(item => item.Id), "Exercises", errors);
        for (var index = 0; index < exercises.Count; index++)
        {
            var exercise = exercises[index];
            if (exercise.Id == Guid.Empty) errors.Add($"Exercises[{index}].Id must not be empty.");
            if (string.IsNullOrWhiteSpace(exercise.Name)) errors.Add($"Exercises[{index}].Name is required.");
            if (!Enum.IsDefined(exercise.Type)) errors.Add($"Exercises[{index}].Type is invalid.");
            if (!Enum.IsDefined(exercise.DefaultUnit)) errors.Add($"Exercises[{index}].DefaultUnit is invalid.");
            if (!Enum.IsDefined(exercise.Category)) errors.Add($"Exercises[{index}].Category is invalid.");
        }
    }

    private static void ValidateTemplates(IReadOnlyList<ExerciseTemplate>? templates, IReadOnlyList<Exercise>? exercises, List<string> errors)
    {
        if (templates is null)
        {
            errors.Add("ExerciseTemplates is required.");
            return;
        }

        var exerciseIds = exercises?.Select(item => item.Id).ToHashSet() ?? [];
        AddDuplicateIdErrors(templates.Select(item => item.Id), "ExerciseTemplates", errors);
        for (var index = 0; index < templates.Count; index++)
        {
            var template = templates[index];
            if (template.Id == Guid.Empty) errors.Add($"ExerciseTemplates[{index}].Id must not be empty.");
            if (string.IsNullOrWhiteSpace(template.Name)) errors.Add($"ExerciseTemplates[{index}].Name is required.");
            if (template.UpdatedAt == default) errors.Add($"ExerciseTemplates[{index}].UpdatedAt must be a valid timestamp.");
            if (template.Items is null)
            {
                errors.Add($"ExerciseTemplates[{index}].Items is required.");
                continue;
            }

            var positions = new HashSet<int>();
            for (var itemIndex = 0; itemIndex < template.Items.Count; itemIndex++)
            {
                var item = template.Items[itemIndex];
                if (!positions.Add(item.Position)) errors.Add($"ExerciseTemplates[{index}].Items has duplicate position {item.Position}.");
                if (!exerciseIds.Contains(item.ExerciseId)) errors.Add($"ExerciseTemplates[{index}].Items[{itemIndex}].ExerciseId does not reference an exercise.");
                if (string.IsNullOrWhiteSpace(item.ExerciseNameSnapshot)) errors.Add($"ExerciseTemplates[{index}].Items[{itemIndex}].ExerciseNameSnapshot is required.");
                if (item.Position < 0) errors.Add($"ExerciseTemplates[{index}].Items[{itemIndex}].Position must not be negative.");
                if (item.TargetSets < 0) errors.Add($"ExerciseTemplates[{index}].Items[{itemIndex}].TargetSets must not be negative.");
                if (item.TargetRepetitions < 0) errors.Add($"ExerciseTemplates[{index}].Items[{itemIndex}].TargetRepetitions must not be negative.");
                if (item.TargetWeightKg < 0) errors.Add($"ExerciseTemplates[{index}].Items[{itemIndex}].TargetWeightKg must not be negative.");
            }
        }
    }

    private static void ValidatePlannedSessions(IReadOnlyList<PlannedSession>? sessions, IReadOnlyList<ExerciseTemplate>? templates, List<string> errors)
    {
        if (sessions is null)
        {
            errors.Add("PlannedSessions is required.");
            return;
        }

        var templateIds = templates?.Select(item => item.Id).ToHashSet() ?? [];
        AddDuplicateIdErrors(sessions.Select(item => item.Id), "PlannedSessions", errors);
        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            if (session.Id == Guid.Empty) errors.Add($"PlannedSessions[{index}].Id must not be empty.");
            if (!templateIds.Contains(session.TemplateId)) errors.Add($"PlannedSessions[{index}].TemplateId does not reference a template.");
            if (string.IsNullOrWhiteSpace(session.TemplateNameSnapshot)) errors.Add($"PlannedSessions[{index}].TemplateNameSnapshot is required.");
            if (session.Position < 0) errors.Add($"PlannedSessions[{index}].Position must not be negative.");
        }
    }

    private static void ValidateWorkoutSessions(IReadOnlyList<WorkoutSessionRecord>? sessions, IReadOnlyList<PlannedSession>? plannedSessions, List<string> errors)
    {
        if (sessions is null)
        {
            errors.Add("WorkoutSessions is required.");
            return;
        }

        var plannedIds = plannedSessions?.Select(item => item.Id).ToHashSet() ?? [];
        AddDuplicateIdErrors(sessions.Select(item => item.Id), "WorkoutSessions", errors);
        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            if (session.Id == Guid.Empty) errors.Add($"WorkoutSessions[{index}].Id must not be empty.");
            if (session.PlannedSessionId is not null && !plannedIds.Contains(session.PlannedSessionId.Value)) errors.Add($"WorkoutSessions[{index}].PlannedSessionId does not reference a planned session.");
            if (string.IsNullOrWhiteSpace(session.TemplateNameSnapshot)) errors.Add($"WorkoutSessions[{index}].TemplateNameSnapshot is required.");
            if (string.IsNullOrWhiteSpace(session.WeightUnit)) errors.Add($"WorkoutSessions[{index}].WeightUnit is required.");
            if (session.StartedAt == default) errors.Add($"WorkoutSessions[{index}].StartedAt must be a valid timestamp.");
            if (session.CompletedAt is not null && session.CompletedAt < session.StartedAt) errors.Add($"WorkoutSessions[{index}].CompletedAt must not precede StartedAt.");
        }
    }

    private static void ValidateWorkoutSets(IReadOnlyList<WorkoutSetRecord>? sets, IReadOnlyList<WorkoutSessionRecord>? sessions, IReadOnlyList<Exercise>? exercises, List<string> errors)
    {
        if (sets is null)
        {
            errors.Add("WorkoutSets is required.");
            return;
        }

        var sessionIds = sessions?.Select(item => item.Id).ToHashSet() ?? [];
        var exerciseIds = exercises?.Select(item => item.Id).ToHashSet() ?? [];
        AddDuplicateIdErrors(sets.Select(item => item.Id), "WorkoutSets", errors);
        var numbersBySession = new Dictionary<Guid, HashSet<int>>();
        for (var index = 0; index < sets.Count; index++)
        {
            var set = sets[index];
            if (set.Id == Guid.Empty) errors.Add($"WorkoutSets[{index}].Id must not be empty.");
            if (!sessionIds.Contains(set.WorkoutSessionId)) errors.Add($"WorkoutSets[{index}].WorkoutSessionId does not reference a workout session.");
            if (!exerciseIds.Contains(set.ExerciseId)) errors.Add($"WorkoutSets[{index}].ExerciseId does not reference an exercise.");
            if (string.IsNullOrWhiteSpace(set.ExerciseNameSnapshot)) errors.Add($"WorkoutSets[{index}].ExerciseNameSnapshot is required.");
            if (set.SetNumber < 0) errors.Add($"WorkoutSets[{index}].SetNumber must not be negative.");
            if (set.WeightKg < 0) errors.Add($"WorkoutSets[{index}].WeightKg must not be negative.");
            if (set.Repetitions < 0) errors.Add($"WorkoutSets[{index}].Repetitions must not be negative.");
            if (string.IsNullOrWhiteSpace(set.Unit)) errors.Add($"WorkoutSets[{index}].Unit is required.");
            if (string.IsNullOrWhiteSpace(set.Status)) errors.Add($"WorkoutSets[{index}].Status is required.");
            if (!numbersBySession.TryGetValue(set.WorkoutSessionId, out var numbers)) numbersBySession[set.WorkoutSessionId] = numbers = [];
            if (!numbers.Add(set.SetNumber)) errors.Add($"WorkoutSets[{index}] has duplicate SetNumber {set.SetNumber} in its session.");
        }
    }

    private static void ValidateActivities(IReadOnlyList<ActivityRecord>? activities, List<string> errors)
    {
        if (activities is null)
        {
            errors.Add("Activities is required.");
            return;
        }

        AddDuplicateIdErrors(activities.Select(item => item.Id), "Activities", errors);
        for (var index = 0; index < activities.Count; index++)
        {
            var activity = activities[index];
            if (activity.Id == Guid.Empty) errors.Add($"Activities[{index}].Id must not be empty.");
            if (activity.RecordedAt == default) errors.Add($"Activities[{index}].RecordedAt must be a valid timestamp.");
            if (string.IsNullOrWhiteSpace(activity.ActivityType)) errors.Add($"Activities[{index}].ActivityType is required.");
            if (activity.DurationSeconds < 0) errors.Add($"Activities[{index}].DurationSeconds must not be negative.");
            if (activity.DistanceMetres < 0) errors.Add($"Activities[{index}].DistanceMetres must not be negative.");
        }
    }

    private static void ValidateRecommendations(IReadOnlyList<Recommendation>? recommendations, IReadOnlyList<Exercise>? exercises, List<string> errors)
    {
        if (recommendations is null)
        {
            errors.Add("Recommendations is required.");
            return;
        }

        var exerciseIds = exercises?.Select(item => item.Id).ToHashSet() ?? [];
        AddDuplicateIdErrors(recommendations.Select(item => item.Id), "Recommendations", errors);
        for (var index = 0; index < recommendations.Count; index++)
        {
            var recommendation = recommendations[index];
            if (recommendation.Id == Guid.Empty) errors.Add($"Recommendations[{index}].Id must not be empty.");
            if (!exerciseIds.Contains(recommendation.ExerciseId)) errors.Add($"Recommendations[{index}].ExerciseId does not reference an exercise.");
            if (string.IsNullOrWhiteSpace(recommendation.ExerciseNameSnapshot)) errors.Add($"Recommendations[{index}].ExerciseNameSnapshot is required.");
            if (string.IsNullOrWhiteSpace(recommendation.RuleKey)) errors.Add($"Recommendations[{index}].RuleKey is required.");
            if (string.IsNullOrWhiteSpace(recommendation.Message)) errors.Add($"Recommendations[{index}].Message is required.");
            if (recommendation.CreatedAt == default) errors.Add($"Recommendations[{index}].CreatedAt must be a valid timestamp.");
        }
    }

    private static void ValidateSettings(IReadOnlyList<UserSettings>? settings, List<string> errors)
    {
        if (settings is null)
        {
            errors.Add("UserSettings is required.");
            return;
        }

        AddDuplicateIdErrors(settings.Select(item => item.Id), "UserSettings", errors);
        for (var index = 0; index < settings.Count; index++)
        {
            var setting = settings[index];
            if (setting.Id == Guid.Empty) errors.Add($"UserSettings[{index}].Id must not be empty.");
            if (!Enum.IsDefined(setting.PreferredUnit)) errors.Add($"UserSettings[{index}].PreferredUnit is invalid.");
            if (string.IsNullOrWhiteSpace(setting.TimeZoneId)) errors.Add($"UserSettings[{index}].TimeZoneId is required.");
            if (setting.UpdatedAt == default) errors.Add($"UserSettings[{index}].UpdatedAt must be a valid timestamp.");
        }
    }

    private static void ValidateActiveWorkout(ActiveWorkoutSnapshot? activeWorkout, List<string> errors)
    {
        if (activeWorkout is null) return;
        if (string.IsNullOrWhiteSpace(activeWorkout.SessionName)) errors.Add("ActiveWorkout.SessionName is required.");
        if (activeWorkout.Sets is null)
        {
            errors.Add("ActiveWorkout.Sets is required.");
            return;
        }

        for (var index = 0; index < activeWorkout.Sets.Count; index++)
        {
            var set = activeWorkout.Sets[index];
            if (string.IsNullOrWhiteSpace(set.Exercise)) errors.Add($"ActiveWorkout.Sets[{index}].Exercise is required.");
            if (set.Weight < 0) errors.Add($"ActiveWorkout.Sets[{index}].Weight must not be negative.");
            if (set.Reps < 0) errors.Add($"ActiveWorkout.Sets[{index}].Reps must not be negative.");
            if (!Enum.IsDefined(set.Status)) errors.Add($"ActiveWorkout.Sets[{index}].Status is invalid.");
        }
    }

    private static void AddDuplicateIdErrors(IEnumerable<Guid> ids, string collectionName, List<string> errors)
    {
        foreach (var duplicate in ids.Where(id => id != Guid.Empty).GroupBy(id => id).Where(group => group.Count() > 1).Select(group => group.Key))
        {
            errors.Add($"{collectionName} contains duplicate Id {duplicate:D}.");
        }
    }
}
