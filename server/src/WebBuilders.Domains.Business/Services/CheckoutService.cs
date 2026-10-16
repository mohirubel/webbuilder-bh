using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Common;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Business.Services;

public interface ICheckoutService
{
    StoreSettings GetSettings();

    /// <summary>Re-checks availability, prices the cart on the server and opens a card payment for it.</summary>
    Task<CheckoutSession> StartCheckoutAsync(IReadOnlyList<(string Domain, int Years)> items, CancellationToken ct);
}

public sealed class CheckoutService(
    IDomainRegistrar registrar,
    IPaymentGateway payments,
    IPricingService pricing) : ICheckoutService
{
    public const int MaxItems = 10;
    public const int MaxYears = 10;

    public StoreSettings GetSettings() => new(
        pricing.Currency,
        pricing.Catalog,
        payments.IsConfigured,
        payments.IsConfigured ? payments.PublishableKey : null,
        registrar.Name,
        registrar.IsLive);

    public async Task<CheckoutSession> StartCheckoutAsync(IReadOnlyList<(string Domain, int Years)> items, CancellationToken ct)
    {
        if (!payments.IsConfigured)
            throw new FeatureUnavailableException("Card payments are not configured on this server. Add Stripe test keys to enable checkout.");

        var cart = BuildCart(items);

        var availability = await registrar.CheckAvailabilityAsync(cart.Select(c => c.Domain).ToList(), ct);
        var unavailable = availability.Where(a => a.Status != AvailabilityStatus.Available).Select(a => a.Domain.Value).ToList();
        if (unavailable.Count > 0)
            throw new BusinessRuleException($"No longer available: {string.Join(", ", unavailable)}. Remove it from your cart and try again.");

        var quote = pricing.CreateQuote(cart);
        var metadata = new Dictionary<string, string>
        {
            [CartCodec.MetadataKey] = CartCodec.Encode(cart),
            ["source"] = "webbuilders-domains"
        };

        var intent = await payments.CreatePaymentIntentAsync(quote.TotalCents, quote.Currency, metadata, ct);
        return new CheckoutSession(intent.Id, intent.ClientSecret, quote);
    }

    private IReadOnlyList<CartItem> BuildCart(IReadOnlyList<(string Domain, int Years)> items)
    {
        if (items is null || items.Count == 0)
            throw new ValidationException("Your cart is empty.");
        if (items.Count > MaxItems)
            throw new ValidationException($"You can buy up to {MaxItems} domains per order.");

        var cart = items.Select(i =>
        {
            if (i.Years is < 1 or > MaxYears)
                throw new ValidationException($"Registration period must be between 1 and {MaxYears} years.");
            var domain = DomainName.Parse(i.Domain);
            pricing.GetYearlyPrice(domain.Tld); // throws for unsupported extensions
            return new CartItem(domain, i.Years);
        }).ToList();

        if (cart.Select(c => c.Domain.Value).Distinct().Count() != cart.Count)
            throw new ValidationException("The same domain appears in your cart more than once.");

        return cart;
    }
}
