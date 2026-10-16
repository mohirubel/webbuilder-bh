using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Business.Abstractions;

/// <summary>Adapter over a domain registrar (Namecheap sandbox, RDAP lookup, ...).
/// Implemented in the data layer; the business layer only knows this contract.</summary>
public interface IDomainRegistrar
{
    string Name { get; }

    /// <summary>True when registrations really happen at the registrar (sandbox counts), false when simulated.</summary>
    bool IsLive { get; }

    Task<IReadOnlyList<DomainAvailability>> CheckAvailabilityAsync(IReadOnlyList<DomainName> domains, CancellationToken ct);

    Task<RegistrationOutcome> RegisterAsync(DomainName domain, int years, Contact registrant, CancellationToken ct);
}

/// <summary>Gateway over a card payment processor. Card details never reach this server:
/// the browser confirms the payment with the processor directly.</summary>
public interface IPaymentGateway
{
    bool IsConfigured { get; }

    string? PublishableKey { get; }

    Task<PaymentIntent> CreatePaymentIntentAsync(long amountCents, string currency, IReadOnlyDictionary<string, string> metadata, CancellationToken ct);

    Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct);

    Task RefundAsync(string paymentIntentId, long amountCents, string idempotencyKey, CancellationToken ct);
}

/// <summary>Repository for completed orders, keyed by payment intent.</summary>
public interface IOrderRepository
{
    Task<Order?> GetByPaymentIntentAsync(string paymentIntentId, CancellationToken ct);

    /// <summary>Claims a payment intent for processing. Returns false when another request already holds it.</summary>
    Task<bool> TryLockAsync(string paymentIntentId, CancellationToken ct);

    Task ReleaseAsync(string paymentIntentId, CancellationToken ct);

    Task SaveAsync(Order order, CancellationToken ct);
}
