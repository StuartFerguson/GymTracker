using System.Globalization;
using GymTracker.Application;
using Microsoft.Maui.Controls.Shapes;

namespace GymTracker.Pages;

internal static class WorkoutNavigationState
{
    public static WorkoutSession? CurrentSession { get; set; }
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
        WorkoutNavigationState.CurrentSession = new WorkoutSession("Upper Body");
        return Shell.Current.GoToAsync(AppRoutes.ActiveWorkout);
    }
}

public sealed class ActiveWorkoutPage : FeaturePage
{
    private readonly WorkoutSession session;
    private readonly Entry weightEntry;
    private readonly Entry repsEntry;
    private readonly Label feedback;
    private readonly VerticalStackLayout setsLayout;

    public ActiveWorkoutPage() : base("Upper Body", "Log each set as you train")
    {
        session = WorkoutNavigationState.CurrentSession ??= new WorkoutSession("Upper Body");

        Body.Children.Add(new Label { Text = "Current exercise", FontFamily = "OpenSansSemibold", FontSize = 17 });
        var exercise = new Picker { Title = "Select exercise", ItemsSource = new[] { "Bench Press", "Barbell Row", "Shoulder Press" }, SelectedIndex = 0 };
        Body.Children.Add(exercise);

        var inputs = new Grid { ColumnDefinitions = Columns(1, 1), ColumnSpacing = 12 };
        weightEntry = new Entry { Placeholder = "Weight", Keyboard = Keyboard.Numeric };
        repsEntry = new Entry { Placeholder = "Reps", Keyboard = Keyboard.Numeric };
        inputs.Add(weightEntry);
        inputs.Add(repsEntry, 1);
        Body.Children.Add(inputs);

        feedback = new Label { TextColor = Color.FromArgb("#B42318") };
        Body.Children.Add(feedback);

        setsLayout = new VerticalStackLayout { Spacing = 8 };
        Body.Children.Add(new Label { Text = "Logged sets", FontFamily = "OpenSansSemibold", FontSize = 17 });
        Body.Children.Add(setsLayout);
        AddAction("Add set", () => AddSet(exercise.SelectedItem?.ToString() ?? "Bench Press"), primary: true);
        AddAction("Finish workout", FinishWorkout);
    }

    private static ColumnDefinitionCollection Columns(double first, double second) =>
        new() { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) };

    private Task AddSet(string exercise)
    {
        if (!decimal.TryParse(weightEntry.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var weight) ||
            !int.TryParse(repsEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var reps))
        {
            feedback.Text = "Enter a valid weight and rep count.";
            return Task.CompletedTask;
        }

        try
        {
            session.AddSet(exercise, weight, reps);
            feedback.Text = string.Empty;
            setsLayout.Children.Add(new Label { Text = $"Set {session.TotalSets}: {weight:g} kg × {reps} reps", FontSize = 16 });
            weightEntry.Text = string.Empty;
            repsEntry.Text = string.Empty;
        }
        catch (ArgumentOutOfRangeException)
        {
            feedback.Text = "Weight and reps must be greater than zero.";
        }

        return Task.CompletedTask;
    }

    private Task FinishWorkout() => Shell.Current.GoToAsync(AppRoutes.WorkoutSummary);
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
