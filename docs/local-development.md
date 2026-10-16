# Local development

Run the website and the Domains API on your own machine against the **sandbox** accounts.

| | Local | Server |
| --- | --- | --- |
| Command | `.\run-local` | `.\deploy-server` |
| URL | <http://localhost:5080> | <https://webbuilder.byteheart.com> |
| Environment | `Development` (set by `launchSettings.json`) | `Production` |
| Settings | `appsettings.json` + `appsettings.Development.json` | `appsettings.json` + generated `appsettings.Production.json` |
| Website served from | `/website` in the repo | `wwwroot` in the uploaded build |
| Keys | .NET user-secrets on your PC | The same user-secrets, written into the build by the deploy script |
| Namecheap `ClientIp` | Your PC's public IP | The server's IP (`deploy/deploy.settings.json`) |

Deploying: [server-deployment.md](server-deployment.md).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2026, or VS Code with the C# Dev Kit (optional)

## 1. Run it

From the repository root:

```powershell
.\run-local
```

It builds, starts the site at <http://localhost:5080> and opens `domains.html` in your browser. Stop it with
**Ctrl+C**. If a copy is already running, it is stopped first, so port 5080 is never "already in use".

Other ways to start it:

- **VS Code:** **Ctrl+Shift+B**, or **Terminal → Run Task… → Run locally**
- **Visual Studio:** open `WebBuilders.slnx`, set **WebBuilders.Domains.Api** as the startup project, press F5
- **By hand:** `cd src/WebBuilders.Domains.Api` then `dotnet run`

Locally the API also serves the website straight from `/website`, so every page works at the same address
(e.g. <http://localhost:5080/index.html>) and HTML/CSS/JS edits show up on refresh.

With no keys, it still runs: search uses public RDAP registry lookups, registrations are simulated and checkout
says payments are not configured. Add the keys below for the full flow.

> If you open the site with VS Code Live Server (port 5500) instead, the page calls the API at
> `http://localhost:5080` automatically. Keep `dotnet run` going alongside it.

## 2. Add the sandbox keys

Keys are stored with [.NET user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), in your
user profile and outside the repository. The deploy script reads the same keys for the server. Run these from
`src/WebBuilders.Domains.Api`:

### Stripe (test mode): card payments

From <https://dashboard.stripe.com/test/apikeys>:

```bash
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
```

### Namecheap sandbox: availability and registration

1. Create an account at <https://www.sandbox.namecheap.com/>.
2. **Profile → Tools → Namecheap API Access**: turn the API on and copy the key.
3. Under **Whitelisted IPs**, add your PC's public IPv4 (see <https://api.ipify.org>) and the server's
   (`208.98.35.107`).
4. Make sure the sandbox account has funds (fake money; new accounts usually do).

```bash
dotnet user-secrets set "Namecheap:ApiUser" "your-sandbox-username"
dotnet user-secrets set "Namecheap:ApiKey" "your-sandbox-api-key"
dotnet user-secrets set "Namecheap:ClientIp" "your-pc-public-ip"
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
| `address already in use` on port 5080 | Something else uses the port. `.\run-local` stops older copies of this app; otherwise close the program using it |
| Keys ignored | The app is not running as `Development` (user-secrets load only there), or it was not restarted |
| Namecheap error 1011150 "Invalid request IP" | Your public IP changed or is not whitelisted |
| `.io` / `.co` show "Unknown" | Namecheap is not configured; these registries have no public RDAP lookup |
