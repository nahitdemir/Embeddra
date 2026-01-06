# Embeddra

Embeddra is a .NET 8 monorepo skeleton for admin/search APIs, a background worker, and shared building blocks.

## Structure

- apps/Embeddra.Admin.Api - Admin API (minimal API)
- apps/Embeddra.Search.Api - Search API (minimal API)
- apps/Embeddra.Worker - Worker service (background jobs + /health)
- src/Embeddra.Shared - Shared class library
- packages/widget - Widget package placeholder
- infra - Infrastructure assets
- docs - Documentation

## Local run (summary)

Build:

```bash
dotnet build
```

Run services:

```bash
dotnet run --project apps/Embeddra.Admin.Api
dotnet run --project apps/Embeddra.Search.Api
dotnet run --project apps/Embeddra.Worker
```

Health checks:

- Admin API: http://localhost:5114/health
- Search API: http://localhost:5222/health
- Worker: http://localhost:5310/health

Ports come from each project's `Properties/launchSettings.json`.
