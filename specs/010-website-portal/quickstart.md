# Quickstart: DaftarX Website + Customer Portal

**Plan**: [plan.md](./plan.md)
**Audience**: A developer cloning the `speckit-co` repo who wants to build + run the website locally and validate the P1 MVP slice end-to-end.

This walks the P1 MVP (marketing pages live + signup + manual paid activation + downloads page) without any P2 dependencies (no support tickets, no multi-user invitations, no Play Store privacy URL guarantees yet — those land in their own task phases).

---

## Prerequisites

1. **.NET 8 SDK** — `dotnet --version` should print `8.0.x`. If not, install from [dot.net](https://dot.net).
2. **Docker Desktop** for SQL Server + Azurite in a local docker-compose stack.
3. **Node.js 20+** (CLI only — for the Tailwind CSS build, not for runtime). Optional if your IDE handles the Tailwind step.
4. **A copy of `vendor-keys.json`** in `<repo root>/vendor-keys.json` — the Ed25519 keypair the licence signer uses. The file is gitignored; pull it from the vendor's password manager. Without it, the paid-licence activation flow throws.

---

## First build

From the repo root:

```pwsh
# Restore + build the new portal projects
dotnet build src/EgyptTax.Portal.Web/EgyptTax.Portal.Web.csproj -c Debug

# Spin up SQL Server + Azurite
docker compose -f deploy/portal/docker-compose.yml up -d

# Apply EF Core migrations (creates the DaftarXPortal database)
dotnet ef database update --project src/EgyptTax.Portal.Infrastructure --startup-project src/EgyptTax.Portal.Web

# Build Tailwind CSS (one-time + watch in another terminal)
cd src/EgyptTax.Portal.Web/wwwroot/css && npx tailwindcss -i input.css -o site.css --watch
```

Run:

```pwsh
dotnet run --project src/EgyptTax.Portal.Web
```

Open `http://localhost:5050` — the homepage should render in Arabic with the gold/charcoal theme.

---

## P1 walkthrough

### Marketing surface

1. **Homepage** at `http://localhost:5050/` — RTL Arabic by default, "ابدأ التجربة" CTA prominent.
2. **Language toggle** → switch to `/en` — page renders LTR English, scroll position preserved.
3. **Pricing** at `/pricing` (or `/ar/pricing` / `/en/pricing`) — four tier cards (Solo / SMB / Enterprise / Firm), each with monthly + annual EGP prices, feature checklist, "Start trial" CTA.
4. **Downloads** at `/downloads` — desktop installer + LAN-client installer + Play Store badge + side-load APK link.
5. **Privacy policy** at `/privacy/android` — renders the vendor's policy (loaded from `Pages/Privacy/Android.cshtml.cs` per FR-006).

Run `tests/EgyptTax.Portal.E2ETests/Marketing/HomepageRenderTests.cs` against `localhost:5050` to verify the SC-006 < 3 s p75 render budget on your machine.

### Signup

1. Click "ابدأ التجربة" on the Solo tier card → land on `/signup`.
2. Fill email + password (≥ 12 chars) + organisation Arabic name + display name → submit.
3. Check the Resend dev console (the local docker-compose runs a fake SMTP that prints emails to stdout) for the email-confirmation link.
4. Click the link → land on `/portal/dashboard` signed in.

### Trial behaviour (no portal involvement)

Per FR-029 / FR-030, the trial happens entirely client-side in the on-prem product. The portal dashboard shows "No active subscription yet — your installed DaftarX is in trial. To subscribe, click below." There's no Trial row in the database — the dashboard inspects whether the organisation has any non-`Cancelled` `Subscription` rows and renders the trial-banner UI when there are none.

### Convert trial → paid (manual activation)

1. From the dashboard, click "اشترك في الخطة" / "Subscribe to a plan".
2. Pick Solo + monthly → land on payment-method selection.
3. Pick "Bank transfer" (no real Paymob integration in dev) — the portal generates an Invoice with `Status = "Pending"` and a vendor-only admin tool can mark it `Paid`.
4. From an admin shell:
   ```pwsh
   dotnet run --project src/EgyptTax.Portal.AdminCli -- mark-invoice-paid INV-2026-00001
   ```
5. Back in the dashboard, the Subscription is now `Active`. Click "Activate paid licence" → paste your machine's HWID (any 16-char hex with dashes works in dev) → click Activate.
6. Click "Download token" → save the `.token` file.
7. Open the on-prem DaftarX (from feature 008) → drop the file at `%PROGRAMDATA%\DaftarX\license\license.token` → restart the service → confirm the dashboard shows the Solo edition with the correct expiry.

This is the SC-002 (signup → installer download) + SC-003 (licence activation) flow end-to-end.

---

## Running the tests

```pwsh
# Unit tier — fast (no DB, no network)
dotnet test tests/EgyptTax.Portal.UnitTests

# Integration tier — needs the docker-compose stack running
dotnet test tests/EgyptTax.Portal.IntegrationTests

# E2E tier — needs the app running on http://localhost:5050
dotnet test tests/EgyptTax.Portal.E2ETests
```

The integration tests use `WebApplicationFactory<Program>` with an in-memory SQLite override + a fake Resend adapter + a fake Paymob HMAC verifier (always returns valid), so they don't need real external credentials.

---

## Common dev pitfalls

- **`vendor-keys.json` missing**: `LicenceSigningService` throws on startup. Drop the file at the repo root from the vendor's password manager. Until then, the homepage + login + signup still work; only the paid-licence activation flow fails.
- **SQL Server connection refused**: docker compose isn't running, or port 1433 is in use by another SQL Server. `docker compose -f deploy/portal/docker-compose.yml ps` to check.
- **Email link 404s when clicked**: the dev environment's `AppBaseUrl` is misconfigured. Check `appsettings.Development.json` — should be `http://localhost:5050`.
- **Marketing page renders English even though browser is Arabic**: cookie `.AspNetCore.Culture` from a previous test is sticky. Open dev tools, delete the cookie, reload.
- **Hot reload not picking up Razor changes**: run with `dotnet watch run` instead of `dotnet run`.
- **Tailwind classes missing**: the `tailwindcss --watch` background task isn't running. Restart it.

---

## Backend dependencies that aren't yet wired

The P1 MVP intentionally STUBS these external integrations so the walkthrough works without real credentials:

- **Paymob** — production uses real HTTPS to Paymob's v3 API. Dev uses a fake adapter that always returns a valid HMAC + treats every `Pending` invoice as payable via the `mark-invoice-paid` admin CLI command.
- **Resend** — production sends real transactional emails. Dev runs a fake SMTP that prints emails to stdout (visible in the `docker compose logs` output).
- **Azure Blob** — production uses real Azure Blob Storage. Dev runs Azurite locally on port 10000.
- **Cloudflare cache purge** — production hits Cloudflare's purge API on deploy. Dev never caches (Cloudflare isn't in the loop locally).

These all swap to their real implementations via DI configuration in `appsettings.Production.json`. Don't commit production credentials — they live in environment variables on the App Service.

---

## Where things live

| Concern | File |
|---------|------|
| Marketing homepage | `src/EgyptTax.Portal.Web/Pages/Index.cshtml` |
| Pricing page | `src/EgyptTax.Portal.Web/Pages/Pricing.cshtml` |
| Downloads page | `src/EgyptTax.Portal.Web/Pages/Downloads.cshtml` |
| Privacy policy (Android) | `src/EgyptTax.Portal.Web/Pages/Privacy/Android.cshtml` |
| Portal dashboard | `src/EgyptTax.Portal.Web/Portal/Pages/Dashboard.razor` |
| Activate paid licence | `src/EgyptTax.Portal.Web/Portal/Pages/Licences/ActivatePaid.razor` |
| Transfer licence | `src/EgyptTax.Portal.Web/Portal/Pages/Licences/Transfer.razor` |
| Licence signing service (wraps existing on-prem flow) | `src/EgyptTax.Portal.Application/Licences/LicenceSigningService.cs` |
| EF Core DbContext | `src/EgyptTax.Portal.Infrastructure/Persistence/PortalDbContext.cs` |
| EF Core migrations | `src/EgyptTax.Portal.Infrastructure/Migrations/` |
| Composition root | `src/EgyptTax.Portal.Web/Program.cs` |
| Docker compose for local stack | `deploy/portal/docker-compose.yml` |
| Cloudflare cache rules | `deploy/portal/cloudflare/cache-rules.json` |

---

## After P1

Once the P1 slice is running end-to-end:

1. Add Support Tickets (US4) — new tables + Razor pages, no impact on existing flow.
2. Add Multi-user invitations (US5) — extend OrganisationMembership + Resend templates.
3. Verify the privacy URL (US6) — at this point the P1 marketing page already serves it; just lock the route in a CI smoke test.
4. Polish — Cloudflare cache rules deployment, SEO structured-data, performance regression tests against the 3-second p75 budget.
