# Web Builders — Domains API

Backend for [`domains.html`](../domains.html): domain search, cart pricing, card checkout and registration.
ASP.NET Core (.NET 10), three-tier, no third-party NuGet packages.

```
server/
├─ WebBuilders.Domains.slnx
└─ src/
   ├─ WebBuilders.Domains.Api        Presentation tier: controllers, DTOs, exception → HTTP mapping, composition root
   ├─ WebBuilders.Domains.Business   Business tier: services, domain models, rules, abstractions (interfaces)
   └─ WebBuilders.Domains.Data       Data tier: registrar adapters, Stripe gateway, order repository
```

Dependencies point inward: `Api → Business ← Data`. The business tier defines the contracts it needs
(`IDomainRegistrar`, `IPaymentGateway`, `IOrderRepository`) and the data tier implements them
(dependency inversion), so a registrar, payment provider or database can be swapped without touching business rules.

| Pattern | Where |
| --- | --- |
| Dependency injection / composition root | `Program.cs`, `AddBusinessLayer`, `AddDataLayer` |
| Adapter | `NamecheapRegistrar`, `RdapRegistrar` behind `IDomainRegistrar` |
| Strategy | `AddDataLayer` picks the Namecheap or RDAP registrar from configuration |
| Gateway | `StripePaymentGateway` behind `IPaymentGateway` |
| Repository | `InMemoryOrderRepository` behind `IOrderRepository` |
| Value object | `DomainName` (validated on creation) |
| Options | `PricingOptions`, `NamecheapOptions`, `StripeOptions`, `RdapOptions` |
| Typed exceptions → Problem Details | `Business/Common/Exceptions.cs` + `Api/Infrastructure/DomainsExceptionHandler.cs` |

## Run it

```bash
cd server/src/WebBuilders.Domains.Api
dotnet run
```

Open <http://localhost:5080/domains.html>. The API also serves the static site, so page and API share one origin.
If you open the page through VS Code Live Server (port 5500), it calls `http://localhost:5080` automatically.

With no keys configured, it still runs: **search works** through public RDAP registry lookups, and checkout says
payments are not configured. Add the sandbox keys below to enable the full flow.

## Sandbox accounts (both free)

### 1. Stripe test mode: card payments

1. Create an account at <https://dashboard.stripe.com/register>. Test mode is on by default.
2. Under **Developers → API keys**, copy the `pk_test_…` and `sk_test_…` keys.
3. Store them as user secrets (kept out of git):

```bash
cd server/src/WebBuilders.Domains.Api
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
```

Only `pk_test_` / `sk_test_` keys are accepted. Test cards:

| Card | Result |
| --- | --- |
| `4242 4242 4242 4242` | Succeeds |
| `4000 0025 0000 3155` | Asks for 3-D Secure, then succeeds |
| `4000 0000 0000 9995` | Declined (insufficient funds) |

Use any future expiry and any CVC.

### 2. Namecheap sandbox: real availability and registration

1. Create an account at <https://www.sandbox.namecheap.com/>.
2. **Profile → Tools → Namecheap API Access**: enable the API and copy the key.
3. Whitelist your server's **public IPv4** on the same page (check it at <https://ifconfig.me>).
4. Add funds to the sandbox account (fake money; registrations are charged against it).
5. Configure:

```bash
dotnet user-secrets set "Namecheap:ApiUser" "your-sandbox-username"
dotnet user-secrets set "Namecheap:ApiKey" "your-sandbox-api-key"
dotnet user-secrets set "Namecheap:ClientIp" "203.0.113.10"
```

When these are set, the Namecheap adapter replaces the RDAP fallback automatically.

Without Namecheap configured, availability comes from each registry's RDAP server (via the
[IANA bootstrap](https://data.iana.org/rdap/dns.json)) and registrations are **simulated**. Some ccTLDs
(e.g. `.io`, `.co`) publish no RDAP, so they show as *Unknown* and cannot be bought until Namecheap is connected.

## API

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/store/config` | Prices, currency, Stripe publishable key, active registrar |
| `GET` | `/api/domains/search?q=mybrand.io` | Availability and price across all extensions |
| `POST` | `/api/checkout/payment-intents` | `{ items: [{ domain, years }] }` → re-checks availability, prices server-side, returns a Stripe client secret |
| `POST` | `/api/orders` | `{ paymentIntentId, contact }` → verifies the payment, registers each domain, refunds any that fail |

Errors use RFC 7807 Problem Details (400 validation, 422 business rule, 409 conflict, 502 upstream, 503 not configured).

## How the money is kept safe

- Card numbers go from the browser straight to Stripe (Stripe Elements). They never reach this server.
- Prices are computed on the server. The cart that was paid for is stored in the payment's metadata, and the order
  is rebuilt from that metadata, not from what the browser sends.
- `/api/orders` checks with Stripe that the payment `succeeded` before registering anything.
- Finalising is idempotent per payment: a retry returns the same order and never registers twice.
- If the registrar rejects a domain after payment, that line is refunded automatically.

## Deploy to Windows Server + IIS

One IIS site serves the whole website **and** the API: on publish, the static pages are copied into `wwwroot`.

### Once per server

1. Install the **ASP.NET Core 10 Hosting Bundle** from <https://dotnet.microsoft.com/download/dotnet/10.0>
   ("Hosting Bundle" under ASP.NET Core Runtime), then restart IIS: `net stop was /y` then `net start w3svc`.
2. In IIS Manager, create an application pool `WebBuilders` with **.NET CLR version = No Managed Code**.
3. Point your existing HTTPS site at the deploy folder (e.g. `C:\inetpub\webbuilders`) and switch it to the `WebBuilders` pool.
4. Set the secrets **on the site**. They are stored in IIS's `applicationHost.config`, so redeploys never overwrite them
   (run in an elevated prompt and replace `Default Web Site` with your site's name):

```bat
set APPCMD=%windir%\system32\inetsrv\appcmd.exe
set SITE=Default Web Site
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Stripe__PublishableKey',value='pk_test_...']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Stripe__SecretKey',value='sk_test_...']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Namecheap__ApiUser',value='your-user']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Namecheap__ApiKey',value='your-key']" /commit:apphost
%APPCMD% set config "%SITE%" -section:system.webServer/aspNetCore /+"environmentVariables.[name='Namecheap__ClientIp',value='SERVER-PUBLIC-IP']" /commit:apphost
```

5. Whitelist the **server's** public IPv4 in the Namecheap sandbox (Profile → Tools → API Access).
   It must match `Namecheap__ClientIp`.

### Every release

```bash
cd server
dotnet publish src/WebBuilders.Domains.Api -c Release -o publish -p:UseAppHost=false
```

Copy the contents of `server/publish` to the site folder on the server. To avoid "file in use" errors, first drop an
`app_offline.htm` file into the site folder (IIS stops the app), copy the new files, then delete `app_offline.htm`.

Then open `https://your-domain/domains.html`. The line under the search box should read
*Registrar: Namecheap Sandbox · Payments: Stripe test mode*.

### Troubleshooting

| Symptom | Fix |
| --- | --- |
| HTTP 500.19 | Hosting Bundle not installed, or IIS not restarted after installing it |
| HTTP 500.30 / 500.31 | Set `stdoutLogEnabled="true"` in the deployed `web.config`, create a `logs` folder, reload, read the log |
| "Registrations are simulated" / Payments "not configured" | Environment variables missing or misspelled (double underscore `__`); recycle the app pool after changing them |
| Namecheap error 1011150 "Invalid request IP" | Server's public IP not whitelisted, or `Namecheap__ClientIp` differs from it |

## Before going to production

- Swap `InMemoryOrderRepository` for a database-backed `IOrderRepository`. Orders are currently lost on restart.
- Add a Stripe webhook (`payment_intent.succeeded`) so orders complete even if the shopper closes the tab after paying.
- Switch to live keys and `https://api.namecheap.com/xml.response` only after a security review.
