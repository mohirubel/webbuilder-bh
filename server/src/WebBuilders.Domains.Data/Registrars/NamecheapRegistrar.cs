using System.Xml.Linq;
using Microsoft.Extensions.Options;
using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Common;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Data.Registrars;

/// <summary>Adapter for the Namecheap XML API (sandbox by default).
/// Docs: https://www.namecheap.com/support/api/methods/</summary>
internal sealed class NamecheapRegistrar(HttpClient http, IOptions<NamecheapOptions> options) : IDomainRegistrar
{
    private const string Provider = "Namecheap";
    private static readonly XNamespace Ns = "http://api.namecheap.com/xml.response";
    private static readonly string[] ContactRoles = ["Registrant", "Tech", "Admin", "AuxBilling"];

    private readonly NamecheapOptions _options = options.Value;

    public string Name => _options.BaseUrl.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
        ? "Namecheap Sandbox"
        : "Namecheap";

    public bool IsLive => true;

    public async Task<IReadOnlyList<DomainAvailability>> CheckAvailabilityAsync(IReadOnlyList<DomainName> domains, CancellationToken ct)
    {
        var lookup = domains.ToDictionary(d => d.Value, StringComparer.OrdinalIgnoreCase);
        var xml = await SendAsync("namecheap.domains.check",
            new() { ["DomainList"] = string.Join(',', domains.Select(d => d.Value)) }, ct);

        var errors = ReadErrors(xml);
        if (errors is not null)
            throw new ExternalServiceException(Provider, errors);

        return xml.Descendants(Ns + "DomainCheckResult")
            .Select(r =>
            {
                var name = (string?)r.Attribute("Domain") ?? "";
                if (!lookup.TryGetValue(name, out var domain)) return null;

                var available = string.Equals((string?)r.Attribute("Available"), "true", StringComparison.OrdinalIgnoreCase);
                var premium = string.Equals((string?)r.Attribute("IsPremiumName"), "true", StringComparison.OrdinalIgnoreCase);

                return premium
                    ? new DomainAvailability(domain, AvailabilityStatus.Taken, "Premium name — not sold online")
                    : new DomainAvailability(domain, available ? AvailabilityStatus.Available : AvailabilityStatus.Taken);
            })
            .OfType<DomainAvailability>()
            .ToList();
    }

    public async Task<RegistrationOutcome> RegisterAsync(DomainName domain, int years, Contact registrant, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["DomainName"] = domain.Value,
            ["Years"] = years.ToString(),
            ["AddFreeWhoisguard"] = "yes",
            ["WGEnabled"] = "yes"
        };

        // Namecheap requires the same set of fields for each contact role.
        foreach (var role in ContactRoles)
        {
            form[$"{role}FirstName"] = registrant.FirstName.Trim();
            form[$"{role}LastName"] = registrant.LastName.Trim();
            form[$"{role}Address1"] = registrant.Address1.Trim();
            form[$"{role}City"] = registrant.City.Trim();
            form[$"{role}StateProvince"] = registrant.StateProvince.Trim();
            form[$"{role}PostalCode"] = registrant.PostalCode.Trim();
            form[$"{role}Country"] = registrant.Country.Trim().ToUpperInvariant();
            form[$"{role}Phone"] = FormatPhone(registrant.Phone);
            form[$"{role}EmailAddress"] = registrant.Email.Trim();
        }

        var xml = await SendAsync("namecheap.domains.create", form, ct);

        var errors = ReadErrors(xml);
        if (errors is not null)
            return new RegistrationOutcome(false, null, errors);

        var result = xml.Descendants(Ns + "DomainCreateResult").FirstOrDefault();
        var registered = string.Equals((string?)result?.Attribute("Registered"), "true", StringComparison.OrdinalIgnoreCase);

        return registered
            ? new RegistrationOutcome(true, (string?)result!.Attribute("OrderID"), $"Registered at {Name}")
            : new RegistrationOutcome(false, null, "The registrar did not confirm the registration.");
    }

    private async Task<XDocument> SendAsync(string command, Dictionary<string, string> parameters, CancellationToken ct)
    {
        var form = new Dictionary<string, string>(parameters)
        {
            ["ApiUser"] = _options.ApiUser,
            ["ApiKey"] = _options.ApiKey,
            ["UserName"] = string.IsNullOrWhiteSpace(_options.UserName) ? _options.ApiUser : _options.UserName,
            ["ClientIp"] = _options.ClientIp,
            ["Command"] = command
        };

        try
        {
            using var response = await http.PostAsync(_options.BaseUrl, new FormUrlEncodedContent(form), ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new ExternalServiceException(Provider, $"HTTP {(int)response.StatusCode}");
            return XDocument.Parse(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException)
        {
            throw new ExternalServiceException(Provider, "The registrar could not be reached.", ex);
        }
    }

    private static string? ReadErrors(XDocument xml)
    {
        var status = (string?)xml.Root?.Attribute("Status");
        if (!string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase)) return null;

        var messages = xml.Descendants(Ns + "Error").Select(e => e.Value.Trim()).Where(m => m.Length > 0).ToList();
        return messages.Count > 0 ? string.Join(" ", messages) : "Unknown registrar error.";
    }

    /// <summary>Namecheap expects +CountryCode.Number, e.g. "+1 555-123-4567" → "+1.5551234567".</summary>
    internal static string FormatPhone(string phone)
    {
        var trimmed = phone.Trim();
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^\+\d{1,3}\.\d{4,}$"))
            return trimmed;

        var groups = System.Text.RegularExpressions.Regex.Matches(trimmed, @"\d+").Select(m => m.Value).ToList();
        if (trimmed.StartsWith('+') && groups.Count > 1)
            return $"+{groups[0]}.{string.Concat(groups.Skip(1))}";

        // No country code given: assume NANP (+1), the registrar will reject anything invalid.
        return $"+1.{string.Concat(groups)}";
    }
}
