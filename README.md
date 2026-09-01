# GymTracker

## Continuous Integration

GitHub Actions validates pull requests targeting `master` and pushes to `master`.
The workflow restores the .NET MAUI workloads and NuGet dependencies, runs the
solution tests, and builds the Android MAUI target.

To run the same checks locally with the .NET 10 SDK, Java 17, and Android SDK
installed:

```powershell
dotnet workload restore GymTracker.slnx
dotnet restore GymTracker.slnx
dotnet test GymTracker.slnx
dotnet build src/GymTracker/GymTracker.csproj --configuration Release --framework net10.0-android --no-restore
```
