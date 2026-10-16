# Architecture

The domain shop is an ASP.NET Core (.NET 10) application split into three tiers. It uses no third-party NuGet
packages; Stripe and Namecheap are called over plain `HttpClient`.

```
src/
├─ WebBuilders.Domains.Api        Presentation tier
│  ├─ Controllers/                HTTP endpoints (thin: map DTO → service call → DTO)
│  ├─ Contracts/                  Request/response DTOs
│  ├─ Infrastructure/             Exception → RFC 7807 Problem Details mapping
│  └─ Program.cs                  Composition root, static site hosting, CORS
├─ WebBuilders.Domains.Business   Business tier
│  ├─ Abstractions/               IDomainRegistrar, IPaymentGateway, IOrderRepository
│  ├─ Models/                     DomainName (value object), Order, Quote, Contact, ...
│  ├─ Services/                   Search, pricing, checkout, order completion
│  └─ Common/                     Typed business exceptions
└─ WebBuilders.Domains.Data       Data tier
   ├─ Registrars/                 NamecheapRegistrar, RdapRegistrar (adapters)
   ├─ Payments/                   StripePaymentGateway
   └─ Repositories/               InMemoryOrderRepository
```

Dependencies point inward: **Api → Business ← Data**. The business tier declares the contracts it needs and the
data tier implements them (dependency inversion), so a registrar, payment provider or database can be replaced
without touching business rules. Only the Api project references both, to wire them together.

## Design patterns

| Pattern | Where |
| --- | --- |
| Dependency injection / composition root | `Program.cs`, `AddBusinessLayer()`, `AddDataLayer()` |
| Adapter | `NamecheapRegistrar`, `RdapRegistrar` behind `IDomainRegistrar` |
| Strategy | `AddDataLayer()` picks Namecheap or RDAP from configuration |
| Gateway | `StripePaymentGateway` behind `IPaymentGateway` |
| Repository | `InMemoryOrderRepository` behind `IOrderRepository` |
| Value object | `DomainName`, validated when created |
| Options | `PricingOptions`, `NamecheapOptions`, `StripeOptions`, `RdapOptions` |
| Typed exceptions → Problem Details | `Business/Common/Exceptions.cs` + `Api/Infrastructure/DomainsExceptionHandler.cs` |

## Purchase flow

1. **Search**: `GET /api/domains/search?q=mybrand`. The registrar checks every extension in the price list.
2. **Checkout**: `POST /api/checkout/payment-intents`. The server re-checks availability, prices the cart
   itself, stores the cart in the Stripe payment's metadata and returns a client secret.
3. **Pay**: the browser confirms the card with Stripe Elements (`confirmCardPayment`), including 3-D Secure.
4. **Complete**: `POST /api/orders`. The server confirms with Stripe that the payment `succeeded`, rebuilds the
   cart from the payment metadata, registers each domain and refunds any domain the registrar rejects.

## API

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/store/config` | Prices, currency, Stripe publishable key, active registrar |
| `GET` | `/api/domains/search?q=mybrand.io` | Availability and price across all extensions |
| `POST` | `/api/checkout/payment-intents` | `{ items: [{ domain, years }] }` → `{ paymentIntentId, clientSecret, amountCents, ... }` |
| `POST` | `/api/orders` | `{ paymentIntentId, contact }` → order with one result per domain |

Errors are RFC 7807 Problem Details:

| Status | Meaning |
| --- | --- |
| 400 | Validation (bad domain name, missing contact field) |
| 409 | The same payment is already being finalised |
| 422 | Business rule (domain taken, payment not completed) |
| 502 | Stripe or Namecheap failed |
| 503 | Payments not configured |

## How payments are kept safe

- Card numbers go from the browser straight to Stripe and never reach this server.
- Prices are computed on the server. The order is rebuilt from the payment's metadata, never from what the browser
  sends after paying.
- Nothing is registered until Stripe confirms the payment `succeeded`.
- Completing an order is idempotent per payment: a retry returns the same order and never registers twice.
- A domain the registrar rejects after payment is refunded automatically.

## Known limits

- Orders are kept in memory and lost on restart or app-pool recycle. Replace `InMemoryOrderRepository` with a
  database-backed `IOrderRepository` before real use.
- If the shopper closes the tab between paying and completion, the order is not finalised. Add a Stripe webhook
  (`payment_intent.succeeded`) that calls `IOrderService.CompleteOrderAsync`.
- `StripeOptions` only accepts `pk_test_` / `sk_test_` keys, by design for the sandbox.
