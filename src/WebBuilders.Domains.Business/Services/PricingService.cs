using Microsoft.Extensions.Options;
using WebBuilders.Domains.Business.Common;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Business.Services;

public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    public string Currency { get; set; } = "usd";

    /// <summary>Extensions on sale. Their order here is the order shown to shoppers.</summary>
    public List<TldPrice> Tlds { get; set; } = [];
}

public sealed class TldPrice
{
    public string Tld { get; set; } = "";

    public long YearlyPriceCents { get; set; }
}

public interface IPricingService
{
    string Currency { get; }

    IReadOnlyList<(string Tld, long YearlyPriceCents)> Catalog { get; }

    bool IsSupported(string tld);

    long GetYearlyPrice(string tld);

    Quote CreateQuote(IReadOnlyList<CartItem> items);
}

public sealed class PricingService(IOptions<PricingOptions> options) : IPricingService
{
    private readonly PricingOptions _options = options.Value;

    private readonly IReadOnlyList<(string Tld, long YearlyPriceCents)> _catalog = options.Value.Tlds
        .Select(t => (t.Tld.Trim().TrimStart('.').ToLowerInvariant(), t.YearlyPriceCents))
        .ToList();

    private Dictionary<string, long> Prices => field ??= _catalog.ToDictionary(c => c.Tld, c => c.YearlyPriceCents);

    public string Currency => _options.Currency.ToLowerInvariant();

    public IReadOnlyList<(string Tld, long YearlyPriceCents)> Catalog => _catalog;

    public bool IsSupported(string tld) => Prices.ContainsKey(tld);

    public long GetYearlyPrice(string tld) =>
        Prices.TryGetValue(tld, out var cents)
            ? cents
            : throw new ValidationException($"Domains ending in .{tld} are not sold here.");

    public Quote CreateQuote(IReadOnlyList<CartItem> items) =>
        new(items.Select(i => new QuoteLine(i.Domain, i.Years, GetYearlyPrice(i.Domain.Tld))).ToList(), Currency);
}
