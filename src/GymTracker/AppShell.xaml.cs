using GymTracker.Application;
using GymTracker.Pages;

namespace GymTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(AppRoutes.StartWorkout, typeof(StartWorkoutPage));
        Routing.RegisterRoute(AppRoutes.ActiveWorkout, typeof(ActiveWorkoutPage));
        Routing.RegisterRoute(AppRoutes.EditWorkoutSet, typeof(EditWorkoutSetPage));
        Routing.RegisterRoute(AppRoutes.WorkoutSummary, typeof(WorkoutSummaryPage));
    }
}
