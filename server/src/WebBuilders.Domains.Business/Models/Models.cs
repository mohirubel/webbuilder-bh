namespace WebBuilders.Domains.Business.Models;

public enum AvailabilityStatus
{
    Available,
    Taken,
    Unknown
}

/// <summary>What a registrar reports for one domain.</summary>
public sealed record DomainAvailability(DomainName Domain, AvailabilityStatus Status, string? Note = null);

/// <summary>One row of a search result, priced for the shopper.</summary>
public sealed record DomainOffer(DomainName Domain, AvailabilityStatus Status, long YearlyPriceCents, string? Note);

public sealed record SearchResult(string Query, string RegistrarName, bool IsLiveRegistrar, IReadOnlyList<DomainOffer> Offers);

public sealed record CartItem(DomainName Domain, int Years);

public sealed record QuoteLine(DomainName Domain, int Years, long YearlyPriceCents)
{
    public long TotalCents => YearlyPriceCents * Years;
}

public sealed record Quote(IReadOnlyList<QuoteLine> Lines, string Currency)
{
    public long TotalCents => Lines.Sum(l => l.TotalCents);
}

public sealed record Contact(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address1,
    string City,
    string StateProvince,
    string PostalCode,
    string Country);

public sealed record RegistrationOutcome(bool Registered, string? OrderId, string? Message);

public enum PaymentStatus
{
    RequiresPaymentMethod,
    RequiresConfirmation,
    RequiresAction,
    Processing,
    Succeeded,
    Canceled,
    Other
}

/// <summary>Provider-neutral view of a card payment.</summary>
public sealed record PaymentIntent(
    string Id,
    string ClientSecret,
    PaymentStatus Status,
    long AmountCents,
    string Currency,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record CheckoutSession(string PaymentIntentId, string ClientSecret, Quote Quote);

public sealed record OrderLine(DomainName Domain, int Years, long PriceCents, bool Registered, string? RegistrarOrderId, string? Message, bool Refunded);

public sealed record Order(
    string PaymentIntentId,
    string RegistrarName,
    bool IsLiveRegistrar,
    Contact Contact,
    IReadOnlyList<OrderLine> Lines,
    long ChargedCents,
    string Currency,
    DateTimeOffset CreatedAt)
{
    public long RefundedCents => Lines.Where(l => l.Refunded).Sum(l => l.PriceCents);
}

public sealed record StoreSettings(
    string Currency,
    IReadOnlyList<(string Tld, long YearlyPriceCents)> Tlds,
    bool PaymentsEnabled,
    string? PaymentPublishableKey,
    string RegistrarName,
    bool IsLiveRegistrar);
