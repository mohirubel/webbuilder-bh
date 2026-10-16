using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Common;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Data.Payments;

/// <summary>Gateway over the Stripe REST API (test mode). Uses Payment Intents so the card number
/// is entered into Stripe Elements in the browser and never touches this server.</summary>
internal sealed class StripePaymentGateway : IPaymentGateway
{
    private const string Provider = "Stripe";

    private readonly HttpClient _http;
    private readonly StripeOptions _options;

    public StripePaymentGateway(HttpClient http, IOptions<StripeOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress ??= new Uri(_options.BaseUrl);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
    }

    public bool IsConfigured => _options.IsConfigured;

    public string? PublishableKey => _options.PublishableKey;

    public async Task<PaymentIntent> CreatePaymentIntentAsync(
        long amountCents, string currency, IReadOnlyDictionary<string, string> metadata, CancellationToken ct)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("amount", amountCents.ToString()),
            new("currency", currency),
            // Payment methods come from Dashboard settings; the page confirms with a card, and
            // redirect-based methods are excluded because the checkout never leaves the page.
            new("automatic_payment_methods[enabled]", "true"),
            new("automatic_payment_methods[allow_redirects]", "never"),
            new("description", "Domain registration")
        };
        form.AddRange(metadata.Select(m => new KeyValuePair<string, string>($"metadata[{m.Key}]", m.Value)));

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/payment_intents") { Content = new FormUrlEncodedContent(form) };
        return Map(await SendAsync(request, ct));
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct)
    {
        if (!paymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
            throw new ValidationException("Invalid payment reference.");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/payment_intents/{Uri.EscapeDataString(paymentIntentId)}");
        return Map(await SendAsync(request, ct));
    }

    public async Task RefundAsync(string paymentIntentId, long amountCents, string idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/refunds")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["payment_intent"] = paymentIntentId,
                ["amount"] = amountCents.ToString(),
                ["reason"] = "requested_by_customer"
            })
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        using var _ = await SendAsync(request, ct);
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new FeatureUnavailableException("Stripe test keys are not configured.");

        try
        {
            using var response = await _http.SendAsync(request, ct);
            var json = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (response.IsSuccessStatusCode) return json;

            using (json)
            {
                var message = json.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var m)
                    ? m.GetString()
                    : null;
                throw new ExternalServiceException(Provider, message ?? $"HTTP {(int)response.StatusCode}");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new ExternalServiceException(Provider, "The payment processor could not be reached.", ex);
        }
    }

    private static PaymentIntent Map(JsonDocument json)
    {
        using (json)
        {
            var root = json.RootElement;
            var metadata = root.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object
                ? md.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "")
                : new Dictionary<string, string>();

            return new PaymentIntent(
                root.GetProperty("id").GetString()!,
                root.GetProperty("client_secret").GetString() ?? "",
                MapStatus(root.GetProperty("status").GetString()),
                root.GetProperty("amount").GetInt64(),
                root.GetProperty("currency").GetString()!,
                metadata);
        }
    }

    private static PaymentStatus MapStatus(string? status) => status switch
    {
        "requires_payment_method" => PaymentStatus.RequiresPaymentMethod,
        "requires_confirmation" => PaymentStatus.RequiresConfirmation,
        "requires_action" => PaymentStatus.RequiresAction,
        "processing" => PaymentStatus.Processing,
        "succeeded" => PaymentStatus.Succeeded,
        "canceled" => PaymentStatus.Canceled,
        _ => PaymentStatus.Other
    };
}
