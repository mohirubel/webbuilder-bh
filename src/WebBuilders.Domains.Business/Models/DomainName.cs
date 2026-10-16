using System.Text.RegularExpressions;
using WebBuilders.Domains.Business.Common;

namespace WebBuilders.Domains.Business.Models;

/// <summary>Value object for a second-level domain such as <c>mybrand.com</c>.</summary>
public sealed partial record DomainName
{
    public string Label { get; }
    public string Tld { get; }
    public string Value => $"{Label}.{Tld}";

    private DomainName(string label, string tld)
    {
        Label = label;
        Tld = tld;
    }

    public static DomainName Create(string label, string tld)
    {
        label = label.Trim().ToLowerInvariant();
        tld = tld.Trim().TrimStart('.').ToLowerInvariant();

        if (!LabelPattern().IsMatch(label))
            throw new ValidationException(
                $"'{label}' is not a valid name. Use 1–63 letters, numbers or hyphens, not starting or ending with a hyphen.");
        if (!TldPattern().IsMatch(tld))
            throw new ValidationException($"'.{tld}' is not a valid extension.");

        return new DomainName(label, tld);
    }

    /// <summary>Parses a full domain (<c>name.tld</c>).</summary>
    public static DomainName Parse(string value)
    {
        var parts = (value ?? "").Trim().ToLowerInvariant().Split('.', 2);
        if (parts.Length != 2)
            throw new ValidationException($"'{value}' is not a full domain name (expected name.tld).");
        return Create(parts[0], parts[1]);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex LabelPattern();

    [GeneratedRegex("^[a-z]{2,24}$")]
    private static partial Regex TldPattern();
}
