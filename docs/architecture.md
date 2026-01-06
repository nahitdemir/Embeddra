# Architecture

Embeddra uses a lightweight clean architecture structure per service without over-engineering.

## Layout

- `apps/Admin`
  - `Embeddra.Admin.Domain` - Domain model and core rules
  - `Embeddra.Admin.Application` - Application use cases
  - `Embeddra.Admin.Infrastructure` - Infra adapters (DB, external services)
  - `Embeddra.Admin.WebApi` - HTTP API (Controllers)
- `apps/Search`
  - `Embeddra.Search.Domain`
  - `Embeddra.Search.Application`
  - `Embeddra.Search.Infrastructure`
  - `Embeddra.Search.WebApi`
- `apps/Worker`
  - `Embeddra.Worker.Application`
  - `Embeddra.Worker.Infrastructure`
  - `Embeddra.Worker.Host` - Background worker host
- `shared/BuildingBlocks` - Logging, correlation, audit, observability

## Dependency direction

Domain -> Application -> Infrastructure -> Host

Shared BuildingBlocks can be referenced by Application/Infrastructure/Host for cross-cutting concerns.

## Conventions

- .NET 8, nullable enabled, implicit global usings
- Controllers live in `WebApi/Controllers`
- Health endpoint exposed from each WebApi/Worker host
