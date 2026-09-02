using System.Globalization;
using GymTracker.Application;
using GymTracker.Core.Domain;
using GymTracker.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;

namespace GymTracker.Pages;

internal static class WorkoutNavigationState
{
    public static WorkoutSession? CurrentSession { get; set; }

    public static ExerciseTemplate? CurrentTemplate { get; set; }

    public static int EditingSetIndex { get; set; } = -1;

    public static ActiveWorkoutRecovery Recovery =>
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<ActiveWorkoutRecovery>()
        ?? throw new InvalidOperationException("The MAUI service provider is not available.");

    public static IWorkoutHistoryRepository WorkoutHistoryRepository =>
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<IWorkoutHistoryRepository>()
        ?? throw new InvalidOperationException("The MAUI service provider is not available.");

    public static ProgressService Progress =>
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<ProgressService>()
        ?? throw new InvalidOperationException("The MAUI service provider is not available.");

    public static BackupService Backup =>
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<BackupService>()
        ?? throw new InvalidOperationException("The MAUI service provider is not available.");
}

public abstract class FeaturePage : ContentPage
{
    protected FeaturePage(string title, string description)
    {
        Title = title;

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(24, 20),
            Spacing = 16
        };

        content.Children.Add(new Label
        {
            Text = title,
            FontSize = 30,
            FontFamily = "OpenSansSemibold",
            TextColor = Color.FromArgb("#14213D")
        });
        content.Children.Add(new Label
        {
            Text = description,
            FontSize = 16,
            TextColor = Color.FromArgb("#5C677D")
        });

        Content = new ScrollView { Content = content };
        Body = content;
    }

    protected VerticalStackLayout Body { get; }

    protected Button AddAction(string text, Func<Task> action, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = primary ? Color.FromArgb("#FCA311") : Color.FromArgb("#14213D"),
            TextColor = primary ? Color.FromArgb("#14213D") : Colors.White,
            FontFamily = "OpenSansSemibold",
            CornerRadius = 12,
            HeightRequest = 50
        };
        button.Clicked += async (_, _) => await action();
        Body.Children.Add(button);
        return button;
    }

    protected void AddSection(string heading, string detail)
    {
        var frame = new Border
        {
            Stroke = Color.FromArgb("#E5E5E5"),
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Padding = 16,
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label { Text = heading, FontFamily = "OpenSansSemibold", FontSize = 17 },
                    new Label { Text = detail, TextColor = Color.FromArgb("#5C677D") }
                }
            }
        };
        Body.Children.Add(frame);
    }

    protected void AddState(FeaturePageState state) => Body.Children.Add(new FeatureStateView(state));
}

public sealed class FeatureStateView : Border
{
    public FeatureStateView(FeaturePageState state)
    {
        Stroke = Color.FromArgb("#E5E5E5");
        StrokeShape = new RoundRectangle { CornerRadius = 14 };
        Padding = 16;
        Content = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = state.Title,
                    FontFamily = "OpenSansSemibold",
                    FontSize = 17,
                    TextColor = ColorFor(state.Kind)
                },
                new Label
                {
                    Text = state.Message,
                    TextColor = Color.FromArgb("#5C677D")
                }
            }
        };
    }

    private static Color ColorFor(FeaturePageStateKind kind) => kind switch
    {
        FeaturePageStateKind.Error => Color.FromArgb("#B42318"),
        FeaturePageStateKind.Empty => Color.FromArgb("#14213D"),
        _ => Color.FromArgb("#5C677D")
    };
}

public sealed class DashboardPage : FeaturePage
{
    public DashboardPage() : base("Good morning", "Your training at a glance")
    {
        AddSection("Today", "Upper Body  •  6 exercises  •  45 min");
        AddAction("Start workout", () => Shell.Current.GoToAsync(AppRoutes.StartWorkout), primary: true);
        AddAction("View weekly plan", () => Shell.Current.GoToAsync(AppRoutes.WeeklyPlan));
        AddAction("Activity log", () => Shell.Current.GoToAsync(AppRoutes.ActivityLog));
        AddAction("History", () => Shell.Current.GoToAsync(AppRoutes.History));
        AddAction("Exercise progress", () => Shell.Current.GoToAsync(AppRoutes.ExerciseProgress));
        AddAction("Backup and settings", () => Shell.Current.GoToAsync(AppRoutes.BackupSettings));
    }
}

public sealed class WeeklyPlanPage : FeaturePage
{
    public WeeklyPlanPage() : base("Weekly plan", "A simple view of the work ahead")
    {
        var catalog = new BuiltInWorkoutCatalog();
        foreach (var day in catalog.WeeklyPlan)
        {
            var detail = day.TemplateName == "Rest"
                ? "Rest day"
                : $"{catalog.GetTemplate(day.TemplateName).Items.Count} exercises";
            AddSection(day.Day.ToString(), $"{day.TemplateName}  •  {detail}");
        }

        AddAction("Start Monday workout", () => Shell.Current.GoToAsync(AppRoutes.StartWorkout), primary: true);
    }
}

public sealed class StartWorkoutPage : FeaturePage
{
    private readonly VerticalStackLayout recoveryLayout;

    public StartWorkoutPage() : base("Start workout", "Choose a session and get straight to your first set")
    {
        recoveryLayout = new VerticalStackLayout { Spacing = 8 };
        Body.Children.Add(recoveryLayout);

        var catalog = new BuiltInWorkoutCatalog();
        foreach (var template in catalog.Templates)
        {
            var templateName = template.Name;
            var exercises = string.Join(", ", template.Items.Select(item => item.ExerciseNameSnapshot));
            AddSection(templateName, exercises);
            AddAction($"Start {templateName}", () => StartSession(catalog, templateName), primary: templateName == "Push");
        }

        AddSection("Quick start", "Begin with an empty session and add exercises as you go");
        AddAction("Start quick workout", () => StartQuickSession());
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        recoveryLayout.Children.Clear();

        try
        {
            var recovery = await WorkoutNavigationState.Recovery.LoadAsync();
            if (recovery is null)
            {
                return;
            }

            recoveryLayout.Children.Add(new Label
            {
                Text = $"In-progress workout: {recovery.Session.Name} ({recovery.Session.TotalSets} sets)",
                FontFamily = "OpenSansSemibold",
                FontSize = 17
            });
            var resume = new Button { Text = "Resume workout" };
            resume.Clicked += async (_, _) =>
            {
                WorkoutNavigationState.CurrentSession = recovery.Session;
                WorkoutNavigationState.CurrentTemplate = recovery.TemplateName is null
                    ? null
                    : new BuiltInWorkoutCatalog().GetTemplate(recovery.TemplateName);
                await Shell.Current.GoToAsync(AppRoutes.ActiveWorkout);
            };
            recoveryLayout.Children.Add(resume);
            var discard = new Button { Text = "Discard saved workout" };
            discard.Clicked += async (_, _) =>
            {
                await WorkoutNavigationState.Recovery.ClearAsync();
                recoveryLayout.Children.Clear();
            };
            recoveryLayout.Children.Add(discard);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            recoveryLayout.Children.Add(new Label
            {
                Text = "Unable to check for a saved workout.",
                TextColor = Color.FromArgb("#B42318")
            });
        }
    }

    private static Task StartSession(BuiltInWorkoutCatalog catalog, string templateName)
    {
        WorkoutNavigationState.CurrentTemplate = catalog.GetTemplate(templateName);
        WorkoutNavigationState.CurrentSession = catalog.StartSession(templateName);
        return Shell.Current.GoToAsync(AppRoutes.ActiveWorkout);
    }

    private static Task StartQuickSession()
    {
        WorkoutNavigationState.CurrentTemplate = null;
        WorkoutNavigationState.CurrentSession = new WorkoutSession("Quick workout");
        return Shell.Current.GoToAsync(AppRoutes.ActiveWorkout);
    }
}

public sealed class ActiveWorkoutPage : FeaturePage
{
    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    private readonly WorkoutSession session;
    private readonly Picker exercisePicker;
    private readonly Entry weightEntry;
    private readonly Entry repsEntry;
    private readonly Entry notesEntry;
    private readonly Picker statusPicker;
    private readonly Label feedback;
    private readonly VerticalStackLayout setsLayout;

    public ActiveWorkoutPage() : base(
        WorkoutNavigationState.CurrentSession?.Name ?? "Workout",
        "Log each set as you train")
    {
        session = WorkoutNavigationState.CurrentSession ??= new WorkoutSession("Quick workout");
        var template = WorkoutNavigationState.CurrentTemplate;
        var exercises = template is null
            ? new[] { "Barbell Bench Press", "Barbell Row", "Overhead Press", "Pull Up" }
            : template.Items.Select(item => item.ExerciseNameSnapshot).ToArray();

        Body.Children.Add(new Label { Text = "Current exercise", FontFamily = "OpenSansSemibold", FontSize = 17 });
        exercisePicker = new Picker
        {
            Title = "Select exercise",
            ItemsSource = exercises,
            SelectedIndex = 0
        };
        Body.Children.Add(exercisePicker);

        var inputs = new Grid { ColumnDefinitions = Columns(1, 1), RowDefinitions = Rows(1, 1), ColumnSpacing = 12, RowSpacing = 8 };
        weightEntry = new Entry { Placeholder = "Weight (optional)", Keyboard = Keyboard.Numeric };
        repsEntry = new Entry { Placeholder = "Reps", Keyboard = Keyboard.Numeric };
        notesEntry = new Entry { Placeholder = "Notes (optional)" };
        inputs.Add(weightEntry);
        inputs.Add(repsEntry, 1);
        inputs.Add(notesEntry, 0, 1);
        statusPicker = new Picker
        {
            Title = "Set status",
            ItemsSource = Enum.GetNames<WorkoutSetStatus>(),
            SelectedItem = WorkoutSetStatus.Completed.ToString()
        };
        inputs.Add(statusPicker, 1, 1);
        Body.Children.Add(inputs);

        feedback = new Label { TextColor = Color.FromArgb("#B42318") };
        Body.Children.Add(feedback);

        setsLayout = new VerticalStackLayout { Spacing = 8 };
        Body.Children.Add(new Label { Text = "Logged sets", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(setsLayout);
        AddAction("Use last session", UseLastSession);
        AddAction("Add set", () => AddSet(exercisePicker.SelectedItem?.ToString() ?? exercises[0]), primary: true);
        AddAction("Finish workout", FinishWorkout);
        AddAction("Cancel workout", CancelWorkout);
    }

    private static ColumnDefinitionCollection Columns(double first, double second) =>
        new() { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) };

    private static RowDefinitionCollection Rows(double first, double second) =>
        new() { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star) };

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshSets();
    }

    private Task UseLastSession()
    {
        var previous = session.GetPreviousSet(exercisePicker.SelectedItem?.ToString() ?? string.Empty);
        if (previous is null)
        {
            feedback.Text = "No previous value is available for this exercise.";
            return Task.CompletedTask;
        }

        weightEntry.Text = previous.Weight?.ToString("g", CultureInfo.InvariantCulture) ?? string.Empty;
        repsEntry.Text = previous.Reps.ToString(CultureInfo.InvariantCulture);
        notesEntry.Text = previous.Notes;
        statusPicker.SelectedItem = previous.Status.ToString();
        feedback.Text = previous.IsPerDumbbell ? "Previous value loaded per dumbbell." : "Previous value loaded.";
        return Task.CompletedTask;
    }

    private async Task AddSet(string exercise)
    {
        if (!TryParseWeight(weightEntry.Text, out var weight))
        {
            feedback.Text = "Enter a valid weight or leave it blank for bodyweight.";
            return;
        }

        var reps = ParseReps(repsEntry.Text);
        var status = ParseStatus(statusPicker.SelectedItem?.ToString());
        if (reps is null)
        {
            feedback.Text = "Enter a valid rep count.";
            return;
        }

        try
        {
            session.AddSet(exercise, weight, reps.Value, notesEntry.Text, status, IsDumbbellExercise(exercise));
            await WorkoutNavigationState.Recovery.SaveAsync(session, WorkoutNavigationState.CurrentTemplate?.Name);
            feedback.Text = string.Empty;
            weightEntry.Text = string.Empty;
            repsEntry.Text = string.Empty;
            notesEntry.Text = string.Empty;
            statusPicker.SelectedItem = WorkoutSetStatus.Completed.ToString();
            RefreshSets();
        }
        catch (ArgumentOutOfRangeException)
        {
            feedback.Text = "Completed sets need reps, and weight must be greater than zero when supplied.";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            feedback.Text = "Set logged, but it could not be saved for recovery.";
            RefreshSets();
        }

        return;
    }

    private void RefreshSets()
    {
        setsLayout.Children.Clear();
        for (var index = 0; index < session.Sets.Count; index++)
        {
            var setIndex = index;
            var set = session.Sets[index];
            var details = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = $"Set {index + 1}  •  {FormatSet(set)}", FontSize = 16, FontFamily = "OpenSansSemibold" },
                    new Label { Text = FormatNotes(set), TextColor = Color.FromArgb("#5C677D"), IsVisible = !string.IsNullOrWhiteSpace(set.Notes) }
                }
            };
            var edit = new Button { Text = "Edit", HeightRequest = 42, WidthRequest = 90, CornerRadius = 10 };
            edit.Clicked += async (_, _) =>
            {
                WorkoutNavigationState.EditingSetIndex = setIndex;
                await Shell.Current.GoToAsync(AppRoutes.EditWorkoutSet);
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
            row.Add(details);
            row.Add(edit, 1);
            setsLayout.Children.Add(new Border
            {
                Stroke = Color.FromArgb("#E5E5E5"),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Padding = 12,
                Content = row
            });
        }
    }

    private static string FormatSet(WorkoutSet set)
    {
        var weight = set.Weight is null ? "Bodyweight" : $"{set.Weight:g} kg{(set.IsPerDumbbell ? " each" : string.Empty)}";
        var reps = set.Reps == 0 ? "no reps" : $"{set.Reps} reps";
        return $"{weight} × {reps}  •  {set.Status}";
    }

    private static string FormatNotes(WorkoutSet set) => $"{set.Notes}";

    private static bool TryParseWeight(string? text, out decimal? weight)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            weight = null;
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            weight = value;
            return true;
        }

        weight = null;
        return false;
    }

    private static int? ParseReps(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 : int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static WorkoutSetStatus ParseStatus(string? text) =>
        Enum.TryParse<WorkoutSetStatus>(text, out var status) ? status : WorkoutSetStatus.Completed;

    private static bool IsDumbbellExercise(string exercise) => exercise.Contains("Dumbbell", StringComparison.OrdinalIgnoreCase);

    private async Task FinishWorkout()
    {
        try
        {
            var catalog = new BuiltInWorkoutCatalog();
            var records = WorkoutHistoryMapping.ToRecords(session, catalog.Exercises, startedAt, DateTimeOffset.UtcNow);
            await WorkoutNavigationState.WorkoutHistoryRepository.SaveAsync(records.Session, records.Sets);
            await Shell.Current.GoToAsync(AppRoutes.WorkoutSummary);
            await WorkoutNavigationState.Recovery.ClearAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            feedback.Text = "The workout could not be saved. Try again before leaving this screen.";
        }
    }

    private async Task CancelWorkout()
    {
        try
        {
            await Shell.Current.GoToAsync("..");
            await WorkoutNavigationState.Recovery.ClearAsync();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            feedback.Text = "Workout cancelled, but the saved recovery state could not be cleared.";
        }
    }
}

public sealed class EditWorkoutSetPage : FeaturePage
{
    private readonly WorkoutSession session;
    private readonly Entry weightEntry;
    private readonly Entry repsEntry;
    private readonly Entry notesEntry;
    private readonly Picker statusPicker;
    private readonly Label feedback;

    public EditWorkoutSetPage() : base("Edit set", "Update the recorded values, then save your changes")
    {
        session = WorkoutNavigationState.CurrentSession ??= new WorkoutSession("Quick workout");
        weightEntry = new Entry { Placeholder = "Weight (optional)", Keyboard = Keyboard.Numeric };
        repsEntry = new Entry { Placeholder = "Reps", Keyboard = Keyboard.Numeric };
        notesEntry = new Entry { Placeholder = "Notes (optional)" };
        statusPicker = new Picker { Title = "Set status", ItemsSource = Enum.GetNames<WorkoutSetStatus>() };
        feedback = new Label { TextColor = Color.FromArgb("#B42318") };

        Body.Children.Add(new Label { Text = "Weight", FontFamily = "OpenSansSemibold" });
        Body.Children.Add(weightEntry);
        Body.Children.Add(new Label { Text = "Reps", FontFamily = "OpenSansSemibold" });
        Body.Children.Add(repsEntry);
        Body.Children.Add(new Label { Text = "Notes", FontFamily = "OpenSansSemibold" });
        Body.Children.Add(notesEntry);
        Body.Children.Add(new Label { Text = "Status", FontFamily = "OpenSansSemibold" });
        Body.Children.Add(statusPicker);
        Body.Children.Add(feedback);
        AddAction("Save changes", Save, primary: true);
        AddAction("Cancel", Cancel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var index = WorkoutNavigationState.EditingSetIndex;
        if (index >= 0 && index < session.Sets.Count)
        {
            var set = session.Sets[index];
            weightEntry.Text = set.Weight?.ToString("g", CultureInfo.InvariantCulture);
            repsEntry.Text = set.Reps.ToString(CultureInfo.InvariantCulture);
            notesEntry.Text = set.Notes;
            statusPicker.SelectedItem = set.Status.ToString();
            feedback.Text = string.Empty;
        }
    }

    private async Task Save()
    {
        var index = WorkoutNavigationState.EditingSetIndex;
        if (index < 0 || index >= session.Sets.Count)
        {
            feedback.Text = "The selected set is no longer available.";
            return;
        }

        var reps = int.TryParse(repsEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedReps) ? parsedReps : -1;
        decimal? weight = string.IsNullOrWhiteSpace(weightEntry.Text)
            ? null
            : decimal.TryParse(weightEntry.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedWeight) ? parsedWeight : -1;
        var status = Enum.TryParse<WorkoutSetStatus>(statusPicker.SelectedItem?.ToString(), out var parsedStatus)
            ? parsedStatus
            : WorkoutSetStatus.Completed;

        try
        {
            session.UpdateSet(index, weight, reps, notesEntry.Text, status, session.Sets[index].IsPerDumbbell);
            try
            {
                await WorkoutNavigationState.Recovery.SaveAsync(session, WorkoutNavigationState.CurrentTemplate?.Name);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                feedback.Text = "Changes applied, but they could not be saved for recovery.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (ArgumentOutOfRangeException)
        {
            feedback.Text = "Completed sets need reps, and weight must be greater than zero when supplied.";
        }
    }

    private Task Cancel() => Shell.Current.GoToAsync("..");
}

public sealed class WorkoutSummaryPage : FeaturePage
{
    public WorkoutSummaryPage() : base("Workout complete", "A quick summary of the work you logged")
    {
        var session = WorkoutNavigationState.CurrentSession ?? new WorkoutSession("Workout");
        AddSection("Total sets", session.TotalSets.ToString(CultureInfo.InvariantCulture));
        AddSection("Training volume", $"{session.TotalVolume:g} kg");
        AddAction("View activity log", () => Shell.Current.GoToAsync(AppRoutes.ActivityLog), primary: true);
        AddAction("Back to dashboard", () => Shell.Current.GoToAsync($"//{AppRoutes.Dashboard}"));
    }
}

public sealed class ActivityLogPage : FeaturePage
{
    private readonly DatePicker datePicker;
    private readonly Picker typePicker;
    private readonly Entry durationEntry;
    private readonly Entry distanceEntry;
    private readonly Entry stepsEntry;
    private readonly Entry notesEntry;
    private readonly Label feedback;
    private readonly VerticalStackLayout summaryLayout;
    private readonly VerticalStackLayout activitiesLayout;

    public ActivityLogPage() : base("Activity log", "Record walking, running, or swimming alongside your workouts")
    {
        datePicker = new DatePicker { Date = DateTime.Today, MaximumDate = DateTime.Today };
        typePicker = new Picker { Title = "Activity type", ItemsSource = Enum.GetNames<ActivityType>(), SelectedIndex = 0 };
        durationEntry = new Entry { Placeholder = "Duration (minutes)", Keyboard = Keyboard.Numeric };
        distanceEntry = new Entry { Placeholder = "Distance (metres)", Keyboard = Keyboard.Numeric };
        stepsEntry = new Entry { Placeholder = "Steps (optional)", Keyboard = Keyboard.Numeric };
        notesEntry = new Entry { Placeholder = "Notes (optional)" };
        feedback = new Label { TextColor = Color.FromArgb("#B42318") };
        summaryLayout = new VerticalStackLayout { Spacing = 8 };
        activitiesLayout = new VerticalStackLayout { Spacing = 8 };

        Body.Children.Add(new Label { Text = "Log an activity", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(datePicker);
        Body.Children.Add(typePicker);
        Body.Children.Add(durationEntry);
        Body.Children.Add(distanceEntry);
        Body.Children.Add(stepsEntry);
        Body.Children.Add(notesEntry);
        Body.Children.Add(feedback);
        AddAction("Save activity", SaveActivity, primary: true);
        Body.Children.Add(new Label { Text = "This week", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(summaryLayout);
        Body.Children.Add(new Label { Text = "Recent activities", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(activitiesLayout);
        AddAction("Start another workout", () => Shell.Current.GoToAsync(AppRoutes.StartWorkout), primary: true);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task SaveActivity()
    {
        if (!TryParseOptionalInt(durationEntry.Text, out var minutes) || !TryParseOptionalDecimal(distanceEntry.Text, out var distance) || !TryParseOptionalInt(stepsEntry.Text, out var steps))
        {
            feedback.Text = "Enter valid non-negative activity values.";
            return;
        }

        try
        {
            var type = Enum.Parse<ActivityType>(typePicker.SelectedItem?.ToString() ?? string.Empty);
            var activity = ActivityLogging.Create(DateOnly.FromDateTime(datePicker.Date ?? DateTime.Today), type, minutes is null ? null : minutes.Value * 60, distance, steps, notesEntry.Text);
            await ActivityRepository.AddAsync(activity);
            durationEntry.Text = string.Empty;
            distanceEntry.Text = string.Empty;
            stepsEntry.Text = string.Empty;
            notesEntry.Text = string.Empty;
            feedback.Text = string.Empty;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            feedback.Text = "The activity could not be saved. Check the values and try again.";
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var weekStart = today.DayOfWeek == DayOfWeek.Sunday ? today.AddDays(-6) : today.AddDays(-(int)today.DayOfWeek + (int)DayOfWeek.Monday);
            var activities = await ActivityRepository.ListAsync(weekStart, weekStart.AddDays(6));
            var summary = ActivityLogging.GetWeeklySummary(activities, weekStart);
            summaryLayout.Children.Clear();
            summaryLayout.Children.Add(new Label { Text = $"{summary.ActivityCount} activities  •  {FormatDuration(summary.TotalDurationSeconds)}  •  {summary.TotalDistanceMetres:g} m" });
            summaryLayout.Children.Add(new Label { Text = $"Walking {summary.CountFor(ActivityType.Walking)}  •  Running {summary.CountFor(ActivityType.Running)}  •  Swimming {summary.CountFor(ActivityType.Swimming)}", TextColor = Color.FromArgb("#5C677D") });

            activitiesLayout.Children.Clear();
            foreach (var activity in activities.OrderByDescending(activity => activity.Date))
            {
                var pace = ActivityLogging.CalculatePace(activity);
                var details = pace is null ? string.Empty : $"  •  {pace.Value:mm\\:ss}/km";
                activitiesLayout.Children.Add(new Label { Text = $"{activity.Date:dd MMM}  •  {activity.Type}  •  {FormatDuration(activity.DurationSeconds)}{details}" });
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            feedback.Text = "Saved activities could not be loaded.";
        }
    }

    private static IActivityRepository ActivityRepository =>
        Microsoft.Maui.Controls.Application.Current?.Handler?.MauiContext?.Services.GetRequiredService<IActivityRepository>()
        ?? throw new InvalidOperationException("The MAUI service provider is not available.");

    private static bool TryParseOptionalInt(string? text, out int? value)
    {
        if (string.IsNullOrWhiteSpace(text)) { value = null; return true; }
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0) { value = parsed; return true; }
        value = null;
        return false;
    }

    private static bool TryParseOptionalDecimal(string? text, out decimal? value)
    {
        if (string.IsNullOrWhiteSpace(text)) { value = null; return true; }
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0) { value = parsed; return true; }
        value = null;
        return false;
    }

    private static string FormatDuration(int? seconds) => seconds is null ? "" : TimeSpan.FromSeconds(seconds.Value).ToString(@"h\:mm", CultureInfo.InvariantCulture);
}

public sealed class HistoryPage : FeaturePage
{
    private readonly DatePicker fromPicker;
    private readonly DatePicker toPicker;
    private readonly Label feedback;
    private readonly VerticalStackLayout metricsLayout;
    private readonly VerticalStackLayout workoutsLayout;
    private readonly VerticalStackLayout activitiesLayout;

    public HistoryPage() : base("History", "Your training streak and completed sessions")
    {
        fromPicker = new DatePicker { Date = DateTime.Today.AddDays(-30), MaximumDate = DateTime.Today };
        toPicker = new DatePicker { Date = DateTime.Today, MaximumDate = DateTime.Today };
        feedback = new Label { TextColor = Color.FromArgb("#B42318") };
        metricsLayout = new VerticalStackLayout { Spacing = 4 };
        workoutsLayout = new VerticalStackLayout { Spacing = 8 };
        activitiesLayout = new VerticalStackLayout { Spacing = 8 };
        Body.Children.Add(new Label { Text = "Date range", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(fromPicker);
        Body.Children.Add(toPicker);
        Body.Children.Add(feedback);
        AddAction("Refresh history", RefreshAsync, primary: true);
        Body.Children.Add(new Label { Text = "Progress summary", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(metricsLayout);
        Body.Children.Add(new Label { Text = "Workouts", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(workoutsLayout);
        Body.Children.Add(new Label { Text = "Activities", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(activitiesLayout);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var report = await WorkoutNavigationState.Progress.GetHistoryAsync(
                DateOnly.FromDateTime(fromPicker.Date ?? DateTime.Today), DateOnly.FromDateTime(toPicker.Date ?? DateTime.Today));
            feedback.Text = string.Empty;
            metricsLayout.Children.Clear();
            metricsLayout.Children.Add(new Label { Text = $"{report.Metrics.WorkoutCount} workouts  •  {report.Metrics.TotalSets} sets  •  {report.Metrics.TotalRepetitions} reps" });
            metricsLayout.Children.Add(new Label { Text = $"{report.Metrics.TrainingVolumeKg:g} kg volume  •  {report.Metrics.ConsistentWeeks} consistent weeks", TextColor = Color.FromArgb("#5C677D") });

            workoutsLayout.Children.Clear();
            foreach (var workout in report.Workouts)
            {
                workoutsLayout.Children.Add(new Label { Text = $"{workout.StartedAt:dd MMM yyyy}  •  {workout.Name}  •  {workout.CompletedSetCount}/{workout.PlannedSetCount} planned sets  •  {workout.TrainingVolumeKg:g} kg" });
            }
            if (report.Workouts.Count == 0) workoutsLayout.Children.Add(new Label { Text = "No workouts in this date range.", TextColor = Color.FromArgb("#5C677D") });

            activitiesLayout.Children.Clear();
            foreach (var activity in report.Activities)
            {
                activitiesLayout.Children.Add(new Label { Text = $"{activity.Date:dd MMM yyyy}  •  {activity.Type}  •  {FormatDuration(activity.DurationSeconds)}  •  {activity.DistanceMetres:g} m" });
            }
            if (report.Activities.Count == 0) activitiesLayout.Children.Add(new Label { Text = "No activities in this date range.", TextColor = Color.FromArgb("#5C677D") });
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            feedback.Text = "History could not be loaded. Check the selected dates and try again.";
        }
    }

    private static string FormatDuration(int seconds) => TimeSpan.FromSeconds(seconds).ToString(@"h\:mm", CultureInfo.InvariantCulture);
}

public sealed class ExerciseProgressPage : FeaturePage
{
    private readonly DatePicker fromPicker;
    private readonly DatePicker toPicker;
    private readonly Label feedback;
    private readonly VerticalStackLayout progressLayout;

    public ExerciseProgressPage() : base("Exercise progress", "Track your strongest lifts over time")
    {
        fromPicker = new DatePicker { Date = DateTime.Today.AddDays(-90), MaximumDate = DateTime.Today };
        toPicker = new DatePicker { Date = DateTime.Today, MaximumDate = DateTime.Today };
        feedback = new Label { TextColor = Color.FromArgb("#B42318") };
        progressLayout = new VerticalStackLayout { Spacing = 8 };
        Body.Children.Add(new Label { Text = "Date range", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(fromPicker);
        Body.Children.Add(toPicker);
        Body.Children.Add(feedback);
        AddAction("Refresh progress", RefreshAsync, primary: true);
        Body.Children.Add(progressLayout);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            var summaries = await WorkoutNavigationState.Progress.GetExerciseProgressAsync(
                DateOnly.FromDateTime(fromPicker.Date ?? DateTime.Today), DateOnly.FromDateTime(toPicker.Date ?? DateTime.Today));
            feedback.Text = string.Empty;
            progressLayout.Children.Clear();
            foreach (var summary in summaries)
            {
                var best = summary.BestWeightKg is null
                    ? $"{summary.BestRepetitions} reps"
                    : $"{summary.BestWeightKg:g} kg × {summary.BestRepetitions} reps";
                progressLayout.Children.Add(new Border
                {
                    Stroke = Color.FromArgb("#E5E5E5"),
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Padding = 16,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label { Text = summary.ExerciseName, FontFamily = "OpenSansSemibold", FontSize = 17 },
                            new Label { Text = $"Personal best: {best}" },
                            new Label { Text = $"{summary.Entries.Count} recorded sets", TextColor = Color.FromArgb("#5C677D") }
                        }
                    }
                });
            }
            if (summaries.Count == 0) progressLayout.Children.Add(new Label { Text = "No exercise progress in this date range.", TextColor = Color.FromArgb("#5C677D") });
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            feedback.Text = "Exercise progress could not be loaded. Check the selected dates and try again.";
        }
    }
}

public sealed class BackupSettingsPage : FeaturePage
{
    private readonly Label feedback;

    public BackupSettingsPage() : base("Backup and settings", "Keep your local training data under your control")
    {
        AddSection("Backup status", "Backups are stored as versioned JSON files in local app storage.");
        feedback = new Label { TextColor = Color.FromArgb("#5C677D") };
        Body.Children.Add(feedback);
        AddAction("Create backup", CreateBackupAsync, primary: true);
        AddAction("Import backup", ImportBackupAsync);
        AddAction("App preferences", () => DisplayAlertAsync("Settings", "Preferences will be available in a later release.", "OK"));
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            var result = await WorkoutNavigationState.Backup.ExportAsync();
            feedback.Text = $"Backup created: {result.FileName} ({result.SizeBytes:N0} bytes).";
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share GymTracker backup",
                File = new ShareFile(result.Path)
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            feedback.Text = "The backup could not be created. Check available storage and try again.";
        }
    }

    private async Task ImportBackupAsync()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a GymTracker backup" });
            if (file is null) return;

            var path = file.FullPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                feedback.Text = "The selected file could not be read.";
                return;
            }

            var validation = await WorkoutNavigationState.Backup.ValidateFileAsync(path);
            if (!validation.IsValid)
            {
                feedback.Text = $"Backup is invalid: {string.Join(" ", validation.Errors)}";
                return;
            }

            var mode = await DisplayActionSheetAsync("Import backup", "Cancel", null, "Replace local data", "Merge with local data");
            if (mode is not ("Replace local data" or "Merge with local data")) return;
            var importMode = mode == "Replace local data" ? BackupImportMode.Replace : BackupImportMode.Merge;
            var confirmed = await DisplayAlertAsync("Confirm import", importMode == BackupImportMode.Replace
                ? "Replace will overwrite local data after creating a recoverable local copy. Continue?"
                : "Merge will keep local data and skip conflicting records. Continue?", "Continue", "Cancel");
            if (!confirmed) return;

            var result = await WorkoutNavigationState.Backup.ImportAsync(path, importMode);
            feedback.Text = result.IsSuccessful
                ? $"Import complete: {result.Mutation!.InsertedRecords:N0} inserted, {result.Mutation.SkippedRecords:N0} skipped."
                : $"Import failed: {string.Join(" ", result.Errors)}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            feedback.Text = "The backup could not be imported. Check the file and available storage, then try again.";
        }
    }
}
