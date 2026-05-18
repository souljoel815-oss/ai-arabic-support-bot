# DaftarX Portal — Deployment Guide

This document describes the production deployment pipeline for the
DaftarX customer portal (feature `010-website-portal`). The portal runs
on **Hostinger Shared Hosting** with PHP 8.4 + MySQL 8.

---

## Production environment variables

Every secret below must be set in `portal/.env` on the server. The
checked-in `portal/.env.example` is the canonical template — copy it
and replace placeholders.

| Variable | Purpose | Rotation cadence |
|----------|---------|------------------|
| `APP_KEY` | Laravel encryption key (sessions, encrypted columns). Generate with `php artisan key:generate`. | Never (would invalidate every encrypted column) |
| `DB_PASSWORD` | MySQL password for the portal database user. | Quarterly |
| `MAIL_PASSWORD` | Resend SMTP API key (`re_xxx…`). | Quarterly |
| `PAYMOB_API_KEY` | Paymob v3 long-lived API key. From Paymob merchant dashboard. | On suspicion of leak; otherwise yearly |
| `PAYMOB_HMAC_SECRET` | Shared secret used to validate webhook signatures. Paymob rotates on request. | When Paymob credentials change |
| `PAYMOB_INTEGRATION_ID_*` | Per-method integration IDs (card / fawry / vodafone_cash / instapay). | When new integration added |
| `LICENCE_SIGNING_KEYS_PATH` | Filesystem path to the Ed25519 keypair JSON used to sign portal-issued paid licences. The matching public key is embedded in the on-prem desktop binary. | Yearly, with coordinated on-prem build |
| `CLOUDFLARE_ZONE_ID` | Cloudflare zone for `daftarx.app`. | Static |
| `CLOUDFLARE_API_TOKEN` | Scoped token: `Zone:Cache Purge`. Used by the post-deploy cache-purge step. | Yearly |
| `UPTIMEROBOT_API_KEY` | Optional — for programmatic status-page updates. | Yearly |

### Hostinger-specific paths

```
~/public_html/                      → web root (point at portal/public/)
~/private/vendor-keys.json          → licence signing keypair (chmod 400)
~/private/.env                      → production .env (chmod 400)
```

`vendor-keys.json` and `.env` **must NOT** live under `public_html/` —
keep them in `~/private/` with `0400` mode so Apache can't serve them.

---

## Deploy pipeline (per push to `main`)

1. **CI builds the artefacts**: composer install (no-dev), `npm run build`, run tests.
2. **rsync via SSH** to Hostinger user account:
   ```
   rsync -az --delete --exclude=.git --exclude=node_modules \
         --exclude=storage/logs --exclude=.env \
         ./portal/ user@hostinger:~/staging/portal/
   ```
3. **Atomic symlink swap** (zero-downtime):
   ```
   ssh user@hostinger 'cd ~ && mv current previous && mv staging current'
   ```
4. **Run migrations + clear caches**:
   ```
   ssh user@hostinger 'cd ~/current/portal && php artisan migrate --force && php artisan optimize'
   ```
5. **Purge Cloudflare cache** (T144):
   ```
   curl -X POST "https://api.cloudflare.com/client/v4/zones/${CLOUDFLARE_ZONE_ID}/purge_cache" \
        -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}" \
        -H "Content-Type: application/json" \
        --data '{"purge_everything":true}'
   ```
6. **Smoke test** (T135): `curl -fsSL https://daftarx.app/privacy/android | grep -q "play_store_required"`.
   Fails the deploy if the route 404s or the expected content is missing.

---

## Status page + uptime monitoring (T146)

- **Status page**: `https://status.daftarx.app` hosted on
  [Statuspage.io](https://statuspage.io) (or self-hosted Uptime Kuma —
  decide before launch).
- **UptimeRobot probes** ping every 5 min from 3 geographies:
  - Cairo (`AS5536`) — primary user base
  - Frankfurt (`AS24940`) — EU latency check
  - US-East (`AS14618`) — global health
- Each probe hits `https://daftarx.app/health` and expects HTTP 200 +
  `"status":"ok"` in the body.
- Alerts route to ops Slack via UptimeRobot's webhook integration.

---

## Cache purge documentation

The Cloudflare API token is a **scoped** token, not the global Account
key. To rotate:

1. In Cloudflare dashboard → My Profile → API Tokens
2. Create token with template "Zone Resources : Cache Purge"
3. Restrict to `daftarx.app` zone only
4. Update `CLOUDFLARE_API_TOKEN` in the deploy CI secret and in
   `portal/.env` on production.
5. Revoke the old token after one successful deploy.

---

## Rollback procedure

If a deploy breaks production:

```bash
ssh user@hostinger 'cd ~ && mv current broken && mv previous current'
ssh user@hostinger 'cd ~/current/portal && php artisan migrate:rollback --step=1 --force'  # only if you ran new migrations
```

Cloudflare cache is purged on the rollback too (re-run the purge step).

---

## Related documentation

- Architecture: `specs/010-website-portal/plan.md`
- API contracts: `specs/010-website-portal/contracts/`
- On-prem licence consumer: `specs/008-egypt-tax-accounting/quickstart.md`
- Android licence consumer: `specs/009-android-app/quickstart.md`
