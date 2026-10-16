# Server deployment (Windows Server + IIS)

One IIS site serves the whole website **and** the Domains API. On publish, the contents of `/website` are copied
into `wwwroot`, so the deploy folder is self-contained.

How the server differs from local development:

| | Local | Server |
| --- | --- | --- |
| Environment | `Development` | `Production` (default when nothing is set) |
| Settings file | `appsettings.json` + `appsettings.Development.json` | `appsettings.json` only |
| Website served from | `/website` in the repo | `wwwroot` in the published folder |
| Keys stored in | .NET user-secrets | Environment variables on the IIS site |
| Namecheap whitelisted IP | Your PC's public IP | **The server's** public IP |
| HTTPS | Not needed | Required (your site's existing certificate) |

For local setup, see [local-development.md](local-development.md).

## One-time server setup

### 1. Install the hosting bundle

Download the **ASP.NET Core 10 Hosting Bundle** from <https://dotnet.microsoft.com/download/dotnet/10.0>
(under *ASP.NET Core Runtime → Windows → Hosting Bundle*), install it, then restart IIS from an elevated prompt:

```bat
net stop was /y
net start w3svc
```

### 2. Create the application pool and site

1. IIS Manager → **Application Pools → Add**: name `WebBuilders`, **.NET CLR version = No Managed Code**.
2. Create the deploy folder, e.g. `C:\inetpub\webbuilders`.
3. Point your existing HTTPS site at that folder (**Basic Settings → Physical path**) and select the `WebBuilders`
   pool. Keep the existing HTTPS binding and certificate.

### 3. Set the keys on the site

Server keys are environment variables on the IIS site. They are stored in IIS's `applicationHost.config`, outside
the deploy folder, so redeploys never overwrite them. Run in an **elevated** Command Prompt and replace the values
and site name:

```bat
set APPCMD=%windir%\system32\inetsrv\appcmd.exe
set SITE=Default Web Site

%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Stripe__PublishableKey',value='pk_test_...']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Stripe__SecretKey',value='sk_test_...']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Namecheap__ApiUser',value='your-sandbox-username']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Namecheap__ApiKey',value='your-sandbox-api-key']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Namecheap__ClientIp',value='SERVER-PUBLIC-IP']" /commit:apphost
```

Names use a **double underscore** (`Stripe__SecretKey`). To change a value later, remove it first with
`/-"environmentVariables.[name='Stripe__SecretKey']"` and add it again.

### 4. Whitelist the server in Namecheap

Find the server's public IPv4 (on the server, open <https://api.ipify.org>) and add it in the Namecheap sandbox:
**Profile → Tools → Namecheap API Access → Whitelisted IPs**. It must equal `Namecheap__ClientIp`.

## Every release

### 1. Publish (on your PC or a build agent)

From the repository root:

```bash
dotnet publish src/WebBuilders.Domains.Api -c Release -o publish -p:UseAppHost=false
```

The `publish` folder contains the API, `web.config` for IIS and the website in `wwwroot`. It contains **no keys**.

### 2. Copy to the server

1. Put an `app_offline.htm` file in the site folder. IIS stops the app, which unlocks its files.
2. Copy everything from `publish` into the site folder, replacing existing files.
3. Delete `app_offline.htm`.

### 3. Check

Open `https://your-domain/domains.html`. Under the search box it should read
*Sandbox · Registrar: Namecheap Sandbox · Payments: Stripe test mode*. Then buy a domain with card
`4242 4242 4242 4242`.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| HTTP 500.19 | Hosting Bundle not installed, or IIS not restarted after installing |
| HTTP 500.30 / 500.31 | In the deployed `web.config` set `stdoutLogEnabled="true"`, create a `logs` folder next to it, reload, read the log |
| Pages return 404 but `/api/store/config` works | `wwwroot` was not copied; publish again and copy the whole folder |
| "Registrations are simulated" / Payments "not configured" | Environment variables missing or misspelled; **recycle the app pool** after changing them |
| Namecheap error 1011150 "Invalid request IP" | Server's IP not whitelisted, or differs from `Namecheap__ClientIp` |

## Going live (real money, real domains)

This setup is sandbox-only. Before taking real customers:

1. Live Stripe and Namecheap accounts and keys, with `Namecheap:BaseUrl` = `https://api.namecheap.com/xml.response`.
2. Relax the test-key check in `StripeOptions.IsConfigured` (Data tier).
3. A database-backed `IOrderRepository` and a Stripe webhook (see [architecture.md](architecture.md#known-limits)).
4. A security review.
