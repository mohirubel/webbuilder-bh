using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Api.Contracts;

// ---- Responses -------------------------------------------------------------

public sealed record TldPriceDto(string Tld, long YearlyPriceCents);

public sealed record StoreConfigDto(
    string Currency,
    IReadOnlyList<TldPriceDto> Tlds,
    bool PaymentsEnabled,
    string? StripePublishableKey,
    string Registrar,
    bool LiveRegistrar)
{
    public static StoreConfigDto From(StoreSettings s) => new(
        s.Currency,
        s.Tlds.Select(t => new TldPriceDto(t.Tld, t.YearlyPriceCents)).ToList(),
        s.PaymentsEnabled,
        s.PaymentPublishableKey,
        s.RegistrarName,
        s.IsLiveRegistrar);
}

public sealed record DomainOfferDto(string Domain, string Tld, string Status, long YearlyPriceCents, string? Note);

public sealed record SearchResponseDto(string Query, string Registrar, bool LiveRegistrar, IReadOnlyList<DomainOfferDto> Results)
{
    public static SearchResponseDto From(SearchResult r) => new(
        r.Query,
        r.RegistrarName,
        r.IsLiveRegistrar,
        r.Offers.Select(o => new DomainOfferDto(
            o.Domain.Value, o.Domain.Tld, o.Status.ToString().ToLowerInvariant(), o.YearlyPriceCents, o.Note)).ToList());
}

public sealed record QuoteLineDto(string Domain, int Years, long YearlyPriceCents, long TotalCents);

public sealed record CheckoutResponseDto(string PaymentIntentId, string ClientSecret, long AmountCents, string Currency, IReadOnlyList<QuoteLineDto> Lines)
{
    public static CheckoutResponseDto From(CheckoutSession s) => new(
        s.PaymentIntentId,
        s.ClientSecret,
        s.Quote.TotalCents,
        s.Quote.Currency,
        s.Quote.Lines.Select(l => new QuoteLineDto(l.Domain.Value, l.Years, l.YearlyPriceCents, l.TotalCents)).ToList());
}

public sealed record OrderLineDto(string Domain, int Years, long PriceCents, bool Registered, string? RegistrarOrderId, string? Message, bool Refunded);

public sealed record OrderResponseDto(
    string PaymentIntentId,
    string Registrar,
    bool LiveRegistrar,
    long ChargedCents,
    long RefundedCents,
    string Currency,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderResponseDto From(Order o) => new(
        o.PaymentIntentId,
        o.RegistrarName,
        o.IsLiveRegistrar,
        o.ChargedCents,
        o.RefundedCents,
        o.Currency,
        o.Lines.Select(l => new OrderLineDto(l.Domain.Value, l.Years, l.PriceCents, l.Registered, l.RegistrarOrderId, l.Message, l.Refunded)).ToList());
}

// ---- Requests --------------------------------------------------------------

public sealed record CartItemDto(string Domain, int Years);

public sealed record CheckoutRequestDto(List<CartItemDto> Items);

public sealed record ContactDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Address1,
    string City,
    string StateProvince,
    string PostalCode,
    string Country)
{
    public Contact ToModel() => new(
        FirstName ?? "", LastName ?? "", Email ?? "", Phone ?? "", Address1 ?? "",
        City ?? "", StateProvince ?? "", PostalCode ?? "", Country ?? "");
}

public sealed record CompleteOrderRequestDto(string PaymentIntentId, ContactDto Contact);
