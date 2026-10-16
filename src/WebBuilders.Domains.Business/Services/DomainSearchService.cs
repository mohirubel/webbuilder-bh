using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Common;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Business.Services;

public interface IDomainSearchService
{
    Task<SearchResult> SearchAsync(string query, CancellationToken ct);
}

public sealed class DomainSearchService(IDomainRegistrar registrar, IPricingService pricing) : IDomainSearchService
{
    public async Task<SearchResult> SearchAsync(string query, CancellationToken ct)
    {
        var (label, requestedTld) = Normalize(query);

        if (requestedTld is not null && !pricing.IsSupported(requestedTld))
            throw new ValidationException(
                $".{requestedTld} is not available here. Try one of: {string.Join(", ", pricing.Catalog.Select(c => "." + c.Tld))}.");

        // The extension the shopper typed comes first, then the rest of the catalogue.
        var tlds = pricing.Catalog.Select(c => c.Tld).ToList();
        if (requestedTld is not null)
        {
            tlds.Remove(requestedTld);
            tlds.Insert(0, requestedTld);
        }

        var candidates = tlds.Select(tld => DomainName.Create(label, tld)).ToList();
        var availability = await registrar.CheckAvailabilityAsync(candidates, ct);
        var byDomain = availability.ToDictionary(a => a.Domain.Value);

        var offers = candidates
            .Select(d => byDomain.TryGetValue(d.Value, out var a)
                ? new DomainOffer(d, a.Status, pricing.GetYearlyPrice(d.Tld), a.Note)
                : new DomainOffer(d, AvailabilityStatus.Unknown, pricing.GetYearlyPrice(d.Tld), "No answer from registrar"))
            .ToList();

        return new SearchResult(candidates[0].Value, registrar.Name, registrar.IsLive, offers);
    }

    /// <summary>Accepts "mybrand", "mybrand.io", "https://www.mybrand.com/path" and returns label + optional TLD.</summary>
    private static (string Label, string? Tld) Normalize(string query)
    {
        var q = (query ?? "").Trim().ToLowerInvariant();
        if (q.Length == 0)
            throw new ValidationException("Type a name to search for.");

        if (q.Contains("://")) q = q[(q.IndexOf("://", StringComparison.Ordinal) + 3)..];
        q = q.Split('/', '?', '#')[0];
        if (q.StartsWith("www.")) q = q[4..];
        q = q.Replace(' ', '-');

        var parts = q.Split('.', 2);
        var label = parts[0];
        var tld = parts.Length == 2 && parts[1].Length > 0 ? parts[1] : null;

        // Validates the label (throws a friendly ValidationException).
        DomainName.Create(label, tld ?? "com");
        return (label, tld);
    }
}
