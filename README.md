# Embeddra

Embeddra is a .NET 8 monorepo skeleton for admin/search APIs, a background worker, and shared building blocks.

## Structure

- apps/Admin - Admin clean architecture (Domain/Application/Infrastructure/WebApi)
- apps/Search - Search clean architecture (Domain/Application/Infrastructure/WebApi)
- apps/Worker - Worker clean architecture (Application/Infrastructure/Host)
- shared/BuildingBlocks - Cross-cutting logging, correlation, audit, observability
- packages/widget - Widget package placeholder
- infra - Infrastructure assets
- docs - Documentation

## Local run (summary)

Run everything (infra + apps) with a single command:

```bash
./dev.sh
```

Stop infra:

```bash
./dev.sh down
```

Build:

```bash
dotnet build
```

Run services individually:

```bash
dotnet run --project apps/Admin/Embeddra.Admin.WebApi
dotnet run --project apps/Search/Embeddra.Search.WebApi
dotnet run --project apps/Worker/Embeddra.Worker.Host
```

Health checks:

- Admin API: http://localhost:5114/health
- Search API: http://localhost:5222/health
- Worker: http://localhost:5310/health

Ports come from each project's `Properties/launchSettings.json`.

Elastic login (local):

- Username: `elastic`
- Password: `embeddra`
