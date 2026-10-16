# Server deployment

The site runs at **<https://webbuilder.byteheart.com>** on Windows/IIS shared hosting (site4now), with .NET 10.
One site serves both the website pages and the Domains API (`/api/...`). Deployment is one command over FTPS.

## How the server differs from local

| | Local | Server |
| --- | --- | --- |
| Command | `dotnet run` (in `src/WebBuilders.Domains.Api`) | `./deploy/deploy.ps1` (from the repo root) |
| URL | <http://localhost:5080> | <https://webbuilder.byteheart.com> |
| Environment | `Development` | `Production` |
| Settings | `appsettings.json` + `appsettings.Development.json` | `appsettings.json` + generated `appsettings.Production.json` |
| Website served from | `/website` in the repo | `wwwroot` in the uploaded build |
| Stripe / Namecheap keys | .NET user-secrets | Same user-secrets, written into the build by the deploy script |
| Namecheap `ClientIp` | Your PC's public IP | `208.98.35.107` (from `deploy/deploy.settings.json`) |

Local setup: [local-development.md](local-development.md).

## Deploy files

```
deploy/
├─ deploy.ps1             Build + upload script
├─ deploy.settings.json   Site URL, FTP host/user, server IP (no secrets; committed)
└─ app_offline.htm        "Updating" page shown during the upload
```

Passwords and keys are **never** stored in the repository:

- **FTP password:** read from the `WB_FTP_PASSWORD` environment variable, or asked for when the script runs.
- **Stripe / Namecheap keys:** read from your local .NET user-secrets (the same ones `dotnet run` uses) and written
  only into `artifacts/publish/appsettings.Production.json`. `artifacts/` is git-ignored.

## One-time setup

1. **Keys on your PC.** Set the Stripe and Namecheap user-secrets as in
   [local-development.md](local-development.md#2-add-the-sandbox-keys). Check with
   `dotnet user-secrets list --project src/WebBuilders.Domains.Api`.
2. **Whitelist the server in Namecheap.** In the sandbox: **Profile → Tools → Namecheap API Access →
   Whitelisted IPs**, add **`208.98.35.107`** (keep your PC's IP too, for local runs).
3. **Hosting control panel.** The site must use **.NET Core** (10.x) hosting. Nothing else needs configuring:
   the build includes its own `web.config`.

## Deploy

From the repository root, in PowerShell:

```powershell
./deploy/deploy.ps1
```

It asks for the FTP password, then:

1. Builds the Release version into `artifacts/publish` (API + website in `wwwroot`).
2. Writes `appsettings.Production.json` with the keys from user-secrets and the server's IP.
3. Uploads `app_offline.htm`, so IIS stops the app and releases its files.
4. Uploads all files over FTPS. Failed files are retried, one at a time.
5. Removes `app_offline.htm`. This always happens, even after an error, so the site never stays on the
   "updating" page.
6. Checks the live site and prints the registrar and payment status.

A successful run ends with:

```
==> Checking https://webbuilder.byteheart.com
  domains.html  : HTTP 200
  Registrar     : Namecheap Sandbox
  Card payments : enabled (Stripe test mode)

Deployed: https://webbuilder.byteheart.com/domains.html
```

Other options:

```powershell
$env:WB_FTP_PASSWORD = '...'; ./deploy/deploy.ps1   # no password prompt (e.g. CI)
./deploy/deploy.ps1 -BuildOnly                      # build into artifacts/publish, upload nothing
```

### Known warning

```
WARNING: Server refused website file: wwwroot\assets\images\avatar\avatar-9.jpg
WARNING: Server refused website file: wwwroot\assets\images\ecommerce\product-5.jpg
```

The host's FTP filter rejects these two sample images from the admin template. No page uses them, so the
warning is harmless. If an **application** file (DLL, config) is refused, the script stops with an error
instead; run it again.

## Changing server settings

| To change | Edit | Then |
| --- | --- | --- |
| Stripe or Namecheap keys | Your user-secrets | Deploy |
| Server IP for Namecheap | `deploy/deploy.settings.json` → `namecheap.serverClientIp` | Deploy |
| FTP host or user, site URL | `deploy/deploy.settings.json` | Deploy |
| Prices, extensions | `src/WebBuilders.Domains.Api/appsettings.json` → `Pricing` | Deploy |

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Search shows "Namecheap: Invalid request IP: x.x.x.x" | Whitelist that IP in the Namecheap sandbox. If it differs from `serverClientIp`, update `deploy.settings.json` and deploy |
| "Missing user-secrets: ..." | Set the listed keys (see local-development.md) |
| curl `(67)` / login failed | Wrong FTP password |
| HTTP 500.30 / 500.31 | In the control panel, check the site uses .NET Core 10; read the logs under the site's `logs` folder (enable `stdoutLogEnabled="true"` in the server's `web.config` temporarily) |
| Site stuck on "We're updating the site" | Delete `app_offline.htm` from the site root over FTP |
| Payments "NOT configured" after deploy | The Stripe keys in user-secrets are missing or not `pk_test_` / `sk_test_` |

## Going live (real money, real domains)

This setup is sandbox-only. Before taking real customers:

1. Live Stripe and Namecheap accounts and keys, with `Namecheap:BaseUrl` = `https://api.namecheap.com/xml.response`.
2. Relax the test-key check in `StripeOptions.IsConfigured` (Data tier).
3. A database-backed `IOrderRepository` and a Stripe webhook (see [architecture.md](architecture.md#known-limits)).
4. A security review.
