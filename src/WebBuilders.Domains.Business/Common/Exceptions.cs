namespace WebBuilders.Domains.Business.Common;

/// <summary>Base type for every failure the business layer reports on purpose.
/// The presentation layer maps each subtype to an HTTP status.</summary>
public abstract class DomainsException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Input is malformed (bad domain name, missing contact field, ...).</summary>
public sealed class ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null)
    : DomainsException(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

/// <summary>Input is well-formed but breaks a business rule (domain already taken, payment not completed, ...).</summary>
public sealed class BusinessRuleException(string message) : DomainsException(message);

/// <summary>The request clashes with work already in progress (e.g. the same payment being finalised twice).</summary>
public sealed class ConflictException(string message) : DomainsException(message);

/// <summary>A required integration is not configured on this server.</summary>
public sealed class FeatureUnavailableException(string message) : DomainsException(message);

/// <summary>A downstream provider (registrar, payment processor) failed or returned an error.</summary>
public sealed class ExternalServiceException(string provider, string message, Exception? inner = null)
    : DomainsException($"{provider}: {message}", inner)
{
    public string Provider { get; } = provider;
}
