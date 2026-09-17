# Repository Guidelines

## Project Structure & Module Organization

GestorEquipos (AssetOps) is an ASP.NET Core 8 MVC equipment lifecycle application with SQL Server and a Spanish-language UI. `Gestor_Equipos.sln` includes the web application and `GestorEquipos.Tests/`.

- `Controllers/`: MVC endpoints; keep business logic in services.
- `Services/`: interfaces, `Implementations/`, and authentication under `Auth/`.
- `Models/`: entities, feature-specific `ViewModels/`, and `DbContext/DbContext.cs` (`MyDbContext`).
- `Views/`: Razor pages grouped by controller; shared layouts in `Views/Shared/`.
- `wwwroot/`: static assets and vendored Bootstrap/jQuery dependencies.
- `Migrations/`: EF Core schema changes.
- `GestorEquipos.Tests/`: service and controller tests; `MANUAL_TESTS.md` records manual scenarios.

## Build, Test, and Development Commands

Install the .NET 8 SDK and configure SQL Server before running locally.

- `dotnet build Gestor_Equipos.sln`: restore dependencies and build both projects.
- `dotnet run --project GestorEquipos.csproj`: start the local web application.
- `dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj`: run automated tests.
- `dotnet test GestorEquipos.Tests/GestorEquipos.Tests.csproj --collect:"XPlat Code Coverage"`: collect coverage with Coverlet.
- `dotnet ef migrations add <Name> --project GestorEquipos.csproj`: generate a migration; requires the `dotnet-ef` tool.

## Coding Style & Naming Conventions

Follow neighboring C# files: four-space indentation, braces on separate lines, PascalCase types/methods/properties, camelCase parameters/locals, and `_camelCase` private fields. Prefix interfaces with `I` and suffix asynchronous methods with `Async`. Preserve nullable annotations.

Match sibling namespaces: models generally use `GestorEquipos.*`, while controllers and services use `Gestor_Equipos.*`. Keep UI text in Spanish and follow existing Razor tag-helper and Bootstrap patterns. No repository-wide formatter or lint configuration is present.

## Testing Guidelines

Use xUnit and fresh EF Core InMemory databases via `TestHelpers.CreateDbContext()`. Name classes `<Service>Tests` and methods `Method_Scenario_ExpectedOutcome`. The documented target is at least 80% line coverage in `Services/`. Add regression tests for business-rule changes and verify affected UI/authorization scenarios using `MANUAL_TESTS.md` with Administrador and Auditor accounts.

## Commit & Pull Request Guidelines

Recent commits use descriptive imperative subjects such as `Add Docker deployment setup for the test server`; no strict Conventional Commits pattern exists. Keep commits focused. Include a PR description, relevant issue links, validation results, migration/configuration notes, and screenshots for UI changes.

## Security & Configuration

Keep credentials in user secrets or environment variables; use `.env.example` for Docker configuration. Configure `ConnectionStrings:DefaultConnection` and initial `BootstrapAdmin:Email`/`BootstrapAdmin:Password`. Startup applies pending migrations, so verify the target database before launching. Never commit secrets or real equipment access credentials.
