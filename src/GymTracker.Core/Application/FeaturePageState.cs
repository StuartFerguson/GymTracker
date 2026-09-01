namespace GymTracker.Application;

public enum FeaturePageStateKind
{
    Loading,
    Empty,
    Error
}

public sealed record FeaturePageState(
    FeaturePageStateKind Kind,
    string Title,
    string Message);

public static class FeaturePageStates
{
    public static FeaturePageState Loading { get; } =
        new(FeaturePageStateKind.Loading, "Loading", "Please wait while this page loads.");

    public static FeaturePageState Empty(string title, string message) =>
        new(FeaturePageStateKind.Empty, title, message);

    public static FeaturePageState Error(string title, string message) =>
        new(FeaturePageStateKind.Error, title, message);
}
