# Web Builders

Marketing website for Web Builders, plus a **domain shop**: visitors search for a domain, add it to a cart and pay
by card. Built with static HTML/Bootstrap and an ASP.NET Core (.NET 10) API in a three-tier architecture.

> **Sandbox only.** Payments use Stripe test mode and domains are registered in the Namecheap sandbox. No real money
> moves and no real domains are bought.

## Repository layout

```
webbuilder-bh/
├─ website/                      Static website (served as-is)
│  ├─ index.html, about.html, … all pages
│  ├─ domains.html               Domain search, cart and card checkout
│  ├─ css/  js/  assets/         Styles, scripts (js/domains.js), images, vendor files
│  └─ admin/                     Admin dashboard template
├─ src/                          Domains API (.NET 10, three tiers)
│  ├─ WebBuilders.Domains.Api        Presentation: controllers, DTOs, error mapping, hosting
│  ├─ WebBuilders.Domains.Business   Business: services, models, rules, abstractions
│  └─ WebBuilders.Domains.Data       Data: Namecheap/RDAP registrars, Stripe gateway, repository
├─ docs/
│  ├─ local-development.md       Run on your PC with the sandbox accounts
│  ├─ server-deployment.md       Deploy to Windows Server + IIS
│  └─ architecture.md            Tiers, design patterns, API, payment safety
├─ WebBuilders.slnx              Solution file
├─ Directory.Build.props         Shared .NET settings
├─ .editorconfig
└─ .gitignore
```

## Quick start (local)

```bash
cd src/WebBuilders.Domains.Api
dotnet run
```

Open <http://localhost:5080/domains.html>. Search works straight away. To enable card checkout and real sandbox
registrations, add the Stripe and Namecheap sandbox keys as described in
**[docs/local-development.md](docs/local-development.md)**.

## Deploy to a server

Publish, then copy the output to IIS:

```bash
dotnet publish src/WebBuilders.Domains.Api -c Release -o publish -p:UseAppHost=false
```

Server setup, keys and release steps: **[docs/server-deployment.md](docs/server-deployment.md)**.

## Local vs server at a glance

| | Local | Server |
| --- | --- | --- |
| Environment | `Development` | `Production` |
| Website served from | `/website` | `wwwroot` in the publish folder |
| Keys | .NET user-secrets | Environment variables on the IIS site |
| Namecheap whitelisted IP | Your PC's public IP | The server's public IP |

Keys are **never** stored in the repository.

## Tech stack

- **Website:** HTML5, Bootstrap 5.3, Bootstrap Icons, vanilla JavaScript
- **API:** ASP.NET Core (.NET 10), controllers, Problem Details, `HttpClient`
- **Payments:** Stripe Payment Intents + Stripe Elements (card), test mode
- **Domains:** Namecheap sandbox API; public RDAP registry lookup as a fallback

## Documentation

- [Local development](docs/local-development.md)
- [Server deployment (IIS)](docs/server-deployment.md)
- [Architecture](docs/architecture.md)
