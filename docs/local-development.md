# Local development

Run the website and the Domains API on your own machine against the **sandbox** accounts.

| | Local | Server |
| --- | --- | --- |
| Environment | `Development` (set by `launchSettings.json`) | `Production` (default) |
| Settings file | `appsettings.json` + `appsettings.Development.json` | `appsettings.json` |
| Website served from | `/website` in the repo | `wwwroot` inside the published folder |
| Keys stored in | .NET user-secrets on your PC | Environment variables on the IIS site |
| URL | <http://localhost:5080> | `https://your-domain` |
| Namecheap whitelisted IP | Your PC's public IP | The server's public IP |

For the server, see [server-deployment.md](server-deployment.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026, or VS Code with the C# Dev Kit (optional)

## 1. Run it

```bash
cd src/WebBuilders.Domains.Api
dotnet run
```

Open <http://localhost:5080/domains.html>. The API also serves the website from `/website`, so every page works at
the same address (e.g. <http://localhost:5080/index.html>).

From Visual Studio: open `WebBuilders.slnx`, set **WebBuilders.Domains.Api** as the startup project, press F5.

With no keys, it still runs: search uses public RDAP registry lookups, registrations are simulated and checkout
says payments are not configured. Add the keys below for the full flow.

> If you open the site with VS Code Live Server (port 5500) instead, the page calls the API at
> `http://localhost:5080` automatically. Keep `dotnet run` going alongside it.

## 2. Add the sandbox keys

Keys are stored with [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), in your
user profile and outside the repository. Run these from `src/WebBuilders.Domains.Api`:

### Stripe (test mode): card payments

From <https://dashboard.stripe.com/test/apikeys>:

```bash
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
```

### Namecheap sandbox: availability and registration

1. Create an account at <https://www.sandbox.namecheap.com/>.
2. **Profile → Tools → Namecheap API Access**: turn the API on and copy the key.
3. Under **Whitelisted IPs**, add your PC's public IPv4 (see <https://api.ipify.org>).
4. Make sure the sandbox account has funds (fake money; new accounts usually do).

```bash
dotnet user-secrets set "Namecheap:ApiUser" "your-sandbox-username"
dotnet user-secrets set "Namecheap:ApiKey" "your-sandbox-api-key"
dotnet user-secrets set "Namecheap:ClientIp" "your-public-ip"
```

Check what is set with `dotnet user-secrets list`. **Restart `dotnet run` after changing keys.**

Under the search box the page shows what is active, e.g.
*Sandbox · Registrar: Namecheap Sandbox · Payments: Stripe test mode*.

## 3. Test a purchase

| Card | Result |
| --- | --- |
| `4242 4242 4242 4242` | Succeeds |
| `4000 0025 0000 3155` | Asks for 3-D Secure, then succeeds |
| `4000 0000 0000 9995` | Declined |

Use any future expiry date and any CVC. Enter the phone number with its country code, e.g. `+61 412 345 678`.

The domain appears in your Namecheap sandbox account and the payment in your Stripe test dashboard.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `address already in use` on port 5080 | Another copy is running. Stop it (`Get-Process WebBuilders.Domains.Api \| Stop-Process`) or close the other terminal |
| "Access is denied" starting the `.exe` | Run the DLL instead: `dotnet bin/Debug/net10.0/WebBuilders.Domains.Api.dll` with `ASPNETCORE_ENVIRONMENT=Development` set |
| Keys ignored | The app is not running as `Development` (user-secrets load only there), or it was not restarted |
| Namecheap error 1011150 "Invalid request IP" | Your public IP changed or is not whitelisted |
| `.io` / `.co` show "Unknown" | Namecheap is not configured; these registries have no public RDAP lookup |
