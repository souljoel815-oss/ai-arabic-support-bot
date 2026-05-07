# `migrations/` — Multi-feature migrations directory

This directory hosts **migration tooling for multiple features**. The repository is polyglot (Python + .NET), and Spec Kit places features side-by-side under `specs/`.

## Layout

| Subpath / file | Owner | Tooling |
| --- | --- | --- |
| `env.py`, `script.py.mako`, `versions/`, `__pycache__/` | A prior Python feature (Alembic) | Alembic — leave untouched. |
| `README.md` (this file) | Feature 008-egypt-tax-accounting | — |
| EF Core migrations for feature 008 | Feature 008-egypt-tax-accounting | EF Core 8 — **lives inside the project** at `src/EgyptTax.Infrastructure/Migrations/` (the .NET convention). This `migrations/` root directory is NOT used to store EF Core migration files; EF Core's default project-relative path is preferred so the application binary carries its own schema. |

## Why EF Core migrations live in `src/EgyptTax.Infrastructure/Migrations/`

- The .NET `dotnet ef` tooling defaults to that path.
- It keeps the feature's schema versioning embedded in the project that owns the `DbContext`, so the deployment artifact ships a coherent (assembly + migrations) bundle.
- It avoids any naming collision with the existing Alembic content in this directory.

## EF Core migration commands (used by feature 008)

```powershell
# Add a migration
dotnet ef migrations add <Name> `
  --project src\EgyptTax.Infrastructure `
  --startup-project src\EgyptTax.Web `
  --context AppDbContext

# Apply pending migrations
dotnet ef database update `
  --project src\EgyptTax.Infrastructure `
  --startup-project src\EgyptTax.Web `
  --context AppDbContext

# Generate idempotent SQL script (for production deployment)
dotnet ef migrations script `
  --project src\EgyptTax.Infrastructure `
  --startup-project src\EgyptTax.Web `
  --idempotent --output egypttax-migrations.sql
```

## Tooling prereq

Restore the local `dotnet-ef` tool first (per `.config/dotnet-tools.json`):

```powershell
dotnet tool restore
```

## Plan reference

See [specs/008-egypt-tax-accounting/plan.md](../specs/008-egypt-tax-accounting/plan.md) §"Project Structure" for the broader source tree, and [specs/008-egypt-tax-accounting/quickstart.md](../specs/008-egypt-tax-accounting/quickstart.md) §4 for the dev-environment migration walkthrough.
