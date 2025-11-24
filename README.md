# DevOps Capstone Project — LogiTrack

## Purpose

LogiTrack is a small logistics tracking sample application built as a capstone
for an Azure Deployment & DevOps training path. The project demonstrates how
to design a web API with persistence, migrations, and lightweight
operational configuration suitable for deployment to cloud platforms.

## Description

LogiTrack provides a simple domain model for orders and inventory items. It
exposes RESTful HTTP endpoints for creating and querying orders and inventory
items and uses SQLite via Entity Framework Core for local development and
testing. The codebase is intentionally small to emphasize deployment, CI/CD,
and infrastructure concerns rather than business logic complexity.

Key features:
- Minimal Web API (ASP.NET Core) with controllers and minimal API endpoints
- EF Core code-first model with migrations and a SQLite provider
- Dependency injection and configuration suitable for local development and
	cloud deployment

## Tech Stack

- .NET 10.0 (ASP.NET Core)
- C# 12 (nullable reference types enabled, implicit usings)
- Entity Framework Core (SQLite provider)
- OpenAPI / built-in OpenAPI support for automatic API docs
- Git for version control

## Project Structure

- `LogiTrack/` — main ASP.NET Core project
	- `Models/` — domain entities (`Order`, `InventoryItem`)
	- `Repositories/` — EF Core `DbContext`
	- `Controllers/` — API controllers
	- `Program.cs` — app startup and minimal API endpoints
	- `Migrations/` — EF Core migrations

## How to run locally

1. Ensure the .NET SDK (10.0) is installed.
2. From the `LogiTrack` folder, restore and build:

```powershell
dotnet restore
dotnet build
```

3. Apply migrations and run (migrations are included in the repository):

```powershell
dotnet ef database update
dotnet run
```

4. The app runs in Development mode and exposes the OpenAPI document at
	 `/openapi/v1.json` and API endpoints under `/api` (for example
	 `/api/orders/{id}`).

## Learnings & Notes

- Demonstrated configuring EF Core with a code-first approach and applying
	migrations to create a local SQLite database.
- Learned best practices for keeping the EF `DbContext` configured for both
	runtime and design-time (migrations) by providing an options-based
	constructor and conditional `OnConfiguring`.
- Implemented one-to-many relationships (Order -> InventoryItem) with
	explicit FK and navigation properties and configured the relationship in
	`OnModelCreating` for clarity.
- Avoided loading large navigation collections unnecessarily by adding
	EF-aware helper methods that set FKs and call `Add`/`AddRange` directly on
	`DbSet<T>`.
- Used a feature branch and pull request workflow to safely introduce API
	endpoints and migrations into `main` without overwriting remote history.

## Next steps / Improvements

- Add integration tests for API endpoints and EF migrations.
- Add CI pipeline (GitHub Actions) to build, run tests, and publish artifacts.
- Add Dockerfile and deployment pipeline to push to Azure App Service or
	Azure Container Apps.

---

If you'd like, I can also add a short README section showing example `curl`
commands to exercise the newly-added API endpoints.
