using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Business.Services;

/// <summary>Stores the cart on the payment intent ("a.com:1,b.io:2") so the order is rebuilt from
/// what was actually paid for, never from what the browser sends afterwards.</summary>
internal static class CartCodec
{
    public const string MetadataKey = "domains";

    public static string Encode(IEnumerable<CartItem> items) =>
        string.Join(',', items.Select(i => $"{i.Domain.Value}:{i.Years}"));

    public static IReadOnlyList<CartItem> Decode(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(':'))
            .Select(p => new CartItem(DomainName.Parse(p[0]), int.Parse(p[1])))
            .ToList();
}
