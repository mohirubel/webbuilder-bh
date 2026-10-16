namespace WebBuilders.Domains.Data;

public sealed class NamecheapOptions
{
    public const string SectionName = "Namecheap";

    public string BaseUrl { get; set; } = "https://api.sandbox.namecheap.com/xml.response";
    public string ApiUser { get; set; } = "";
    public string ApiKey { get; set; } = "";

    /// <summary>Account that owns the registrations; defaults to <see cref="ApiUser"/>.</summary>
    public string UserName { get; set; } = "";

    /// <summary>Public IPv4 of this server. It must be whitelisted under Profile → Tools → API Access in the sandbox.</summary>
    public string ClientIp { get; set; } = "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiUser) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ClientIp);
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string BaseUrl { get; set; } = "https://api.stripe.com/";
    public string PublishableKey { get; set; } = "";
    public string SecretKey { get; set; } = "";

    /// <summary>Only test-mode keys are accepted: this integration is for the sandbox.</summary>
    public bool IsConfigured =>
        PublishableKey.StartsWith("pk_test_", StringComparison.Ordinal) &&
        SecretKey.StartsWith("sk_test_", StringComparison.Ordinal);
}

public sealed class RdapOptions
{
    public const string SectionName = "Rdap";

    /// <summary>IANA file that maps each TLD to its registry's RDAP server.</summary>
    public string BootstrapUrl { get; set; } = "https://data.iana.org/rdap/dns.json";
}
