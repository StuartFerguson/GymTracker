using System.Globalization;
using GymTracker.Application;
using Microsoft.Maui.Controls.Shapes;

namespace GymTracker.Pages;

internal static class WorkoutNavigationState
{
    public static WorkoutSession? CurrentSession { get; set; }

    public static int EditingSetIndex { get; set; } = -1;
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
        AddSection("Monday", "Upper Body  •  6 exercises");
        AddSection("Wednesday", "Lower Body  •  5 exercises");
        AddSection("Friday", "Full Body  •  7 exercises");
        AddAction("Start Monday workout", () => Shell.Current.GoToAsync(AppRoutes.StartWorkout), primary: true);
    }
}

public sealed class StartWorkoutPage : FeaturePage
{
    public StartWorkoutPage() : base("Start workout", "Choose a session and get straight to your first set")
    {
        AddSection("Upper Body", "Bench press, row, shoulder press, pulldown");
        AddAction("Start Upper Body", StartSession, primary: true);
        AddSection("Quick start", "Begin with an empty session and add exercises as you go");
        AddAction("Start quick workout", StartSession);
    }

    private static Task StartSession()
    {
        WorkoutNavigationState.CurrentSession = new WorkoutSession("Upper Body",
        [
            new WorkoutSet("Bench Press", 60, 10),
            new WorkoutSet("Dumbbell Bench Press", 22.5m, 10, IsPerDumbbell: true),
            new WorkoutSet("Pull Up", null, 8)
        ]);
        return Shell.Current.GoToAsync(AppRoutes.ActiveWorkout);
    }
}

public sealed class ActiveWorkoutPage : FeaturePage
{
    private readonly WorkoutSession session;
    private readonly Picker exercisePicker;
    private readonly Entry weightEntry;
    private readonly Entry repsEntry;
    private readonly Entry notesEntry;
    private readonly Picker statusPicker;
    private readonly Label feedback;
    private readonly VerticalStackLayout setsLayout;

    public ActiveWorkoutPage() : base("Upper Body", "Log each set as you train")
    {
        session = WorkoutNavigationState.CurrentSession ??= new WorkoutSession("Upper Body");

        Body.Children.Add(new Label { Text = "Current exercise", FontFamily = "OpenSansSemibold", FontSize = 17 });
        exercisePicker = new Picker
        {
            Title = "Select exercise",
            ItemsSource = new[] { "Bench Press", "Barbell Row", "Shoulder Press", "Dumbbell Bench Press", "Pull Up" },
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
        AddAction("Add set", () => AddSet(exercisePicker.SelectedItem?.ToString() ?? "Bench Press"), primary: true);
        AddAction("Finish workout", FinishWorkout);
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

    private Task AddSet(string exercise)
    {
        if (!TryParseWeight(weightEntry.Text, out var weight))
        {
            feedback.Text = "Enter a valid weight or leave it blank for bodyweight.";
            return Task.CompletedTask;
        }

        var reps = ParseReps(repsEntry.Text);
        var status = ParseStatus(statusPicker.SelectedItem?.ToString());
        if (reps is null)
        {
            feedback.Text = "Enter a valid rep count.";
            return Task.CompletedTask;
        }

        try
        {
            session.AddSet(exercise, weight, reps.Value, notesEntry.Text, status, IsDumbbellExercise(exercise));
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

        return Task.CompletedTask;
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

    private Task FinishWorkout() => Shell.Current.GoToAsync(AppRoutes.WorkoutSummary);
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
        session = WorkoutNavigationState.CurrentSession ??= new WorkoutSession("Upper Body");
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

    private Task Save()
    {
        var index = WorkoutNavigationState.EditingSetIndex;
        if (index < 0 || index >= session.Sets.Count)
        {
            feedback.Text = "The selected set is no longer available.";
            return Task.CompletedTask;
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
            return Shell.Current.GoToAsync("..");
        }
        catch (ArgumentOutOfRangeException)
        {
            feedback.Text = "Completed sets need reps, and weight must be greater than zero when supplied.";
            return Task.CompletedTask;
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
    public ActivityLogPage() : base("Activity log", "Recent training sessions and notes")
    {
        AddSection("Today", "Upper Body  •  In progress");
        AddSection("Yesterday", "Rest day");
        AddSection("Monday", "Lower Body  •  18 sets  •  4,860 kg");
        AddAction("Start another workout", () => Shell.Current.GoToAsync(AppRoutes.StartWorkout), primary: true);
    }
}

public sealed class HistoryPage : FeaturePage
{
    public HistoryPage() : base("History", "Your training streak and completed sessions")
    {
        AddSection("This week", "2 workouts  •  36 sets");
        AddSection("This month", "9 workouts  •  162 sets");
        AddSection("Current streak", "3 weeks consistent");
    }
}

public sealed class ExerciseProgressPage : FeaturePage
{
    public ExerciseProgressPage() : base("Exercise progress", "Track your strongest lifts over time")
    {
        AddSection("Bench Press", "65 kg × 8  •  +5 kg this month");
        AddSection("Barbell Row", "60 kg × 10  •  +2.5 kg this month");
        AddSection("Shoulder Press", "32.5 kg × 8  •  New best");
    }
}

public sealed class BackupSettingsPage : FeaturePage
{
    public BackupSettingsPage() : base("Backup and settings", "Keep your local training data under your control")
    {
        AddSection("Backup status", "Last backup: Not configured");
        AddAction("Create backup", () => DisplayAlertAsync("Backup", "Backup will be available when persistence is added.", "OK"), primary: true);
        AddAction("App preferences", () => DisplayAlertAsync("Settings", "Preferences will be available in a later release.", "OK"));
    }
}
