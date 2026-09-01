# GymTracker App Scaffold Design

## Goal

Create the initial Android-first .NET MAUI application structure for GymTracker, with clear boundaries for UI, application orchestration, domain logic, and persistence while preserving future iOS portability.

## Scope

This design covers the solution and project scaffold, startup composition, initial navigation shell, and test project setup. It does not implement SQLite persistence, workout features, activity logging, progression rules, backup, accounts, cloud services, or external health integrations.

## Architecture

The solution contains one MAUI app project under `src/GymTracker` and one test project under `tests/GymTracker.Tests`. The app project is organized into `Domain`, `Application`, `Infrastructure`, and `UI` areas so later issues can add behavior without coupling screens directly to storage.

The initial composition root registers application services and platform-facing abstractions through MAUI dependency injection. Persistence is represented only by boundaries needed for the scaffold; SQLite implementation belongs to issue #2. The app remains fully local and has no backend or account flow.

## Platform Targets

- Android is the first runnable target.
- The MAUI project retains iOS-compatible target structure for later enablement.
- Platform-specific behavior is isolated behind MAUI or application interfaces.

## Startup and Navigation

The app starts through the standard MAUI builder and `App` instance. A shell-based navigation root is established with placeholder routes for the first-release journeys, allowing later issues to replace placeholders independently.

## Testing

The test project verifies the scaffold's non-visual composition contract: the app project and test project build together, and the application composition can be constructed without a backend or account dependency. Feature behavior and persistence tests are owned by their respective later issues.

## Acceptance Criteria Mapping

- .NET MAUI project is present and configured for Android.
- UI, application/domain logic, and infrastructure boundaries are represented by the project structure.
- No backend or account flow is introduced.
- The solution can be extended for iOS and future integrations without moving core logic into platform-specific code.
- A test project is available for subsequent unit and integration coverage.
