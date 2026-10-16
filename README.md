# Web Builders

Marketing website for Web Builders, plus a **domain shop**: visitors search for a domain, add it to a cart and pay
by card. Built with static HTML/Bootstrap and an ASP.NET Core (.NET 10) API in a three-tier architecture.

**Live:** <https://webbuilder.byteheart.com> · domain shop: <https://webbuilder.byteheart.com/domains.html>

> **Sandbox only.** Payments use Stripe test mode and domains are registered in the Namecheap sandbox. No real money
> moves and no real domains are bought.

## Repository layout

```
webbuilder-bh/
├─ website/                      Static website
│  ├─ index.html, about.html, … all pages
│  ├─ domains.html               Domain search, cart and card checkout
│  ├─ css/  js/  assets/         Styles, scripts (js/domains.js), images, vendor files
│  └─ admin/                     Admin dashboard template
├─ src/                          Domains API (.NET 10, three tiers)
│  ├─ WebBuilders.Domains.Api        Presentation: controllers, DTOs, error mapping, hosting
│  ├─ WebBuilders.Domains.Business   Business: services, models, rules, abstractions
│  └─ WebBuilders.Domains.Data       Data: Namecheap/RDAP registrars, Stripe gateway, repository
├─ deploy/                       Server deployment
│  ├─ deploy.ps1                 Build + upload over FTPS (one command)
│  ├─ deploy.settings.json       Site URL, FTP host/user, server IP (no secrets)
│  └─ app_offline.htm            "Updating" page shown during upload
├─ docs/
│  ├─ local-development.md       Run on your PC with the sandbox accounts
│  ├─ server-deployment.md       Deploy to webbuilder.byteheart.com
│  └─ architecture.md            Tiers, design patterns, API, payment safety
├─ WebBuilders.slnx              Solution file
├─ Directory.Build.props         Shared .NET settings
├─ .editorconfig
└─ .gitignore                    Also ignores artifacts/ (build output)
```

One site serves everything: the .NET app hosts the website pages and the `/api` endpoints together, locally and on
the server.

## Local development

```bash
cd src/WebBuilders.Domains.Api
dotnet run
```

Open <http://localhost:5080/domains.html>. Search works straight away. To enable card checkout and real sandbox
registrations, add the Stripe and Namecheap sandbox keys as user-secrets: **[docs/local-development.md](docs/local-development.md)**.

## Server deployment

From the repository root, in PowerShell:

```powershell
./deploy/deploy.ps1
```

It asks for the FTP password, builds the Release version, adds the server keys from your user-secrets, uploads over
FTPS and checks the live site. Details and troubleshooting: **[docs/server-deployment.md](docs/server-deployment.md)**.

## Local vs server at a glance

| | Local | Server |
| --- | --- | --- |
| Command | `dotnet run` | `./deploy/deploy.ps1` |
| URL | <http://localhost:5080> | <https://webbuilder.byteheart.com> |
| Environment | `Development` | `Production` |
| Website served from | `/website` | `wwwroot` in the uploaded build |
| Keys | .NET user-secrets | Same user-secrets, written into the build at deploy time |
| Namecheap whitelisted IP | Your PC's public IP | `208.98.35.107` |

Passwords and keys are **never** stored in the repository.

## Tech stack

- **Website:** HTML5, Bootstrap 5.3, Bootstrap Icons, vanilla JavaScript
- **API:** ASP.NET Core (.NET 10), controllers, Problem Details, `HttpClient`
- **Payments:** Stripe Payment Intents + Stripe Elements (card), test mode
- **Domains:** Namecheap sandbox API; public RDAP registry lookup as a fallback
- **Hosting:** Windows/IIS (site4now), deployed over FTPS

## Documentation

- [Local development](docs/local-development.md)
- [Server deployment](docs/server-deployment.md)
- [Architecture](docs/architecture.md)
