using GymTracker.Application;

namespace GymTracker;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        foreach (var route in AppRoutes.All.Skip(1))
        {
            Routing.RegisterRoute(route, typeof(MainPage));
        }
    }
}
