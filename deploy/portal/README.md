# DaftarX Portal — Deployment Runbook

Operational runbook for the `010-website-portal` feature (the `daftarx.app` marketing site + customer portal). For the architectural rationale see [`specs/010-website-portal/research.md`](../../specs/010-website-portal/research.md); for what's deployed where, see this file.

---

## Architecture

Single ASP.NET Core 8 process serving two surfaces:

- `daftarx.app/*` — public Razor Pages marketing surface, cached at the Cloudflare edge.
- `daftarx.app/portal/*` — authenticated Blazor Server customer portal, never cached.

Behind:

- **SQL Server** — the new `DaftarXPortal` database (commercial data only — Subscriptions, Licences, Invoices, Tickets, Members, Audit Log). Physically separate from the on-prem product's `EgyptTax` database per FR-032.
- **Azure Blob Storage** — invoice PDFs + ticket attachments. Azurite locally.
- **Paymob** — payment processor (card + Fawry + Vodafone Cash + InstaPay).
- **Resend** — transactional email.
- **Cloudflare** — CDN + DDoS + cache rules per [`cloudflare/cache-rules.json`](./cloudflare/cache-rules.json).

---

## Local dev stack

```pwsh
docker compose -f deploy/portal/docker-compose.yml up -d
docker compose -f deploy/portal/docker-compose.yml logs -f portal-web
```

Portal at `http://localhost:5050`. SQL Server at `localhost,1433` (sa / `Daftarx_local_DEV_password_1!`). Azurite blob at `http://localhost:10000`.

For pure-IDE work without Docker, [`appsettings.Development.json`](../../src/EgyptTax.Portal.Web/appsettings.Development.json) defaults to a SQLite file `daftarx-portal-dev.db` so you can `dotnet run` straight from the IDE.

---

## Production deploy

1. CI builds the Docker image via [`Dockerfile`](./Dockerfile) (multi-stage: Tailwind → .NET publish → distroless runtime).
2. CI pushes the image to the vendor's container registry.
3. CI applies EF Core migrations against the production SQL Server.
4. CI rolls the App Service to the new image with zero-downtime swap.
5. CI hits the Cloudflare purge API per [`cloudflare/cache-rules.json#purge_on_deploy`](./cloudflare/cache-rules.json) to invalidate stale marketing HTML.
6. CI smoke-tests `/health` + `/privacy/android` + the homepage from outside the cluster.

---

## Production secrets / env vars

Set these on the production App Service — **never** commit to git.

| Variable | Purpose |
|---|---|
| `ConnectionStrings__PortalDb` | SQL Server connection string. |
| `Paymob__ApiKey` | Paymob v3 API key. |
| `Paymob__HmacSecret` | Webhook HMAC signing secret. |
| `Paymob__IntegrationIdCard` / `IntegrationIdFawry` / `IntegrationIdInstaPay` / `IntegrationIdVodafoneCash` | Paymob integration IDs per payment method. |
| `Resend__ApiKey` | Resend transactional API key. |
| `AzureBlob__ConnectionString` | Azure Storage account connection string. |
| `Cloudflare__ZoneId` | Cloudflare zone id (read-only — used for cache purge target). |
| `Cloudflare__ApiToken` | Cloudflare API token with `Cache Purge` scope. |
| `ApplicationInsights__ConnectionString` | App Insights for Serilog sink + request telemetry. |
| `LicenceSigning__VendorKeysPath` | Path to `vendor-keys.json` mounted as a secret file. |

Rotate the Paymob HMAC, Resend, and Cloudflare tokens annually. Rotate the SA / managed-identity DB credentials on a regulator-driven schedule.

---

## Secret rotation

1. Generate new credential in the vendor system (Paymob console / Resend dashboard / Azure Portal / Cloudflare dashboard).
2. Set the new value on the App Service as a slot-specific env var on the **staging** slot.
3. Verify staging passes the smoke tests.
4. Swap staging → production (zero-downtime).
5. Revoke the old credential.

---

## Incident response

| Symptom | First step |
|---|---|
| Marketing pages slow (> 3 s p75 from Cairo) | Check Cloudflare edge cache hit ratio in the dashboard. If < 90 %, check whether a recent deploy caused a cache-rule regression. |
| Signup form returns 500 | Check Application Insights for the failing request — most common cause is a `Resend` 4xx (verify domain still verified). |
| Paymob webhook 401s | Verify the HMAC secret on App Service matches the one in the Paymob console. If rotated on Paymob without updating App Service, every webhook fails until updated. |
| Licence activation hangs | Verify `vendor-keys.json` is mounted at the path in `LicenceSigning__VendorKeysPath`. If the file is missing, `LicenceSigningService` throws on every call. |
| `/privacy/android` returns 4xx/5xx | Critical — the Android Play Store listing depends on this URL (feature 009 FR-018 + SC-004). Roll back the deploy immediately. |

---

## Backup + restore

- **SQL Server**: daily backups to a separate storage account, 30-day retention per research §14.
- **Blob storage**: soft-delete enabled with 30-day retention; ticket attachments + invoice PDFs are restorable for 30 days after delete.
- **App config**: env-var values are tracked in the vendor's password manager (not in git); rebuilding from scratch requires the password-manager export.

---

## Related docs

- Architecture: [`../../specs/010-website-portal/plan.md`](../../specs/010-website-portal/plan.md)
- Research decisions: [`../../specs/010-website-portal/research.md`](../../specs/010-website-portal/research.md)
- Data model: [`../../specs/010-website-portal/data-model.md`](../../specs/010-website-portal/data-model.md)
- Contracts: [`../../specs/010-website-portal/contracts/`](../../specs/010-website-portal/contracts/)
- Local quickstart: [`../../specs/010-website-portal/quickstart.md`](../../specs/010-website-portal/quickstart.md)
