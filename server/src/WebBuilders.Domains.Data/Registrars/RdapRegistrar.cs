using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Data.Registrars;

/// <summary>Fallback used when no registrar sandbox is configured: real availability straight from each
/// registry's public RDAP server (found through the IANA bootstrap file), with simulated registrations.</summary>
internal sealed class RdapRegistrar(HttpClient http, IMemoryCache cache, IOptions<RdapOptions> options) : IDomainRegistrar
{
    private const string BootstrapCacheKey = "rdap:iana-bootstrap";

    public string Name => "RDAP lookup (simulated registration)";

    public bool IsLive => false;

    public async Task<IReadOnlyList<DomainAvailability>> CheckAvailabilityAsync(IReadOnlyList<DomainName> domains, CancellationToken ct)
    {
        var servers = await GetBootstrapAsync(ct);
        return await Task.WhenAll(domains.Select(d => LookupAsync(d, servers, ct)));
    }

    public Task<RegistrationOutcome> RegisterAsync(DomainName domain, int years, Contact registrant, CancellationToken ct)
    {
        var reference = $"SIM-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        return Task.FromResult(new RegistrationOutcome(
            true, reference, "Simulated registration. Configure the Namecheap sandbox to register for real."));
    }

    private async Task<DomainAvailability> LookupAsync(DomainName domain, IReadOnlyDictionary<string, string> servers, CancellationToken ct)
    {
        // Not every registry runs RDAP (e.g. .io, .co): no server means we simply cannot tell.
        if (!servers.TryGetValue(domain.Tld, out var baseUrl))
            return new DomainAvailability(domain, AvailabilityStatus.Unknown,
                $"No public registry lookup for .{domain.Tld}. Connect the registrar sandbox to check it.");

        try
        {
            using var response = await http.GetAsync($"{baseUrl}domain/{domain.Value}", HttpCompletionOption.ResponseHeadersRead, ct);
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => new DomainAvailability(domain, AvailabilityStatus.Available),
                HttpStatusCode.OK => new DomainAvailability(domain, AvailabilityStatus.Taken),
                HttpStatusCode.TooManyRequests => new DomainAvailability(domain, AvailabilityStatus.Unknown, "Registry is busy, try again shortly"),
                _ => new DomainAvailability(domain, AvailabilityStatus.Unknown, $"Registry answered {(int)response.StatusCode}")
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new DomainAvailability(domain, AvailabilityStatus.Unknown, "Registry did not respond");
        }
    }

    /// <summary>TLD → RDAP base URL, from https://data.iana.org/rdap/dns.json (cached for a day).</summary>
    private async Task<IReadOnlyDictionary<string, string>> GetBootstrapAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(BootstrapCacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
            return cached;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var stream = await http.GetStreamAsync(options.Value.BootstrapUrl, ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // "services": [ [ ["com","net"], ["https://rdap.verisign.com/com/v1/"] ], ... ]
            foreach (var service in json.RootElement.GetProperty("services").EnumerateArray())
            {
                var url = service[1].EnumerateArray()
                    .Select(u => u.GetString() ?? "")
                    .OrderByDescending(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(url)) continue;
                if (!url.EndsWith('/')) url += "/";

                foreach (var tld in service[0].EnumerateArray())
                    map[tld.GetString() ?? ""] = url;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            // Keep going with an empty map: every result becomes "unknown" instead of failing the search.
            cache.Set(BootstrapCacheKey, (IReadOnlyDictionary<string, string>)map, TimeSpan.FromMinutes(1));
            return map;
        }

        cache.Set(BootstrapCacheKey, (IReadOnlyDictionary<string, string>)map, TimeSpan.FromHours(24));
        return map;
    }
}
