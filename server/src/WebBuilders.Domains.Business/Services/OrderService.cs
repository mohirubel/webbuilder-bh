using System.Net.Mail;
using Microsoft.Extensions.Logging;
using WebBuilders.Domains.Business.Abstractions;
using WebBuilders.Domains.Business.Common;
using WebBuilders.Domains.Business.Models;

namespace WebBuilders.Domains.Business.Services;

public interface IOrderService
{
    /// <summary>Called after the browser confirmed the card payment: verifies it with the processor,
    /// registers each domain and refunds any domain the registrar rejects. Safe to call twice.</summary>
    Task<Order> CompleteOrderAsync(string paymentIntentId, Contact registrant, CancellationToken ct);
}

public sealed class OrderService(
    IDomainRegistrar registrar,
    IPaymentGateway payments,
    IPricingService pricing,
    IOrderRepository orders,
    TimeProvider clock,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<Order> CompleteOrderAsync(string paymentIntentId, Contact registrant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ValidationException("Missing payment reference.");
        ValidateContact(registrant);

        if (await orders.GetByPaymentIntentAsync(paymentIntentId, ct) is { } existing)
            return existing;

        if (!await orders.TryLockAsync(paymentIntentId, ct))
            throw new ConflictException("This order is already being processed.");

        try
        {
            // Re-check after taking the lock in case a parallel request just finished.
            if (await orders.GetByPaymentIntentAsync(paymentIntentId, ct) is { } finished)
                return finished;

            var intent = await payments.GetPaymentIntentAsync(paymentIntentId, ct);
            if (intent.Status != PaymentStatus.Succeeded)
                throw new BusinessRuleException("The card payment has not been completed.");
            if (!intent.Metadata.TryGetValue(CartCodec.MetadataKey, out var encodedCart))
                throw new BusinessRuleException("This payment does not belong to a domain order.");

            var lines = new List<OrderLine>();
            foreach (var item in CartCodec.Decode(encodedCart))
                lines.Add(await RegisterLineAsync(intent, item, registrant, ct));

            var order = new Order(
                intent.Id, registrar.Name, registrar.IsLive, registrant, lines,
                intent.AmountCents, intent.Currency, clock.GetUtcNow());

            await orders.SaveAsync(order, ct);
            return order;
        }
        finally
        {
            await orders.ReleaseAsync(paymentIntentId, ct);
        }
    }

    private async Task<OrderLine> RegisterLineAsync(PaymentIntent intent, CartItem item, Contact registrant, CancellationToken ct)
    {
        var price = pricing.GetYearlyPrice(item.Domain.Tld) * item.Years;

        RegistrationOutcome outcome;
        try
        {
            outcome = await registrar.RegisterAsync(item.Domain, item.Years, registrant, ct);
        }
        catch (ExternalServiceException ex)
        {
            logger.LogError(ex, "Registration of {Domain} failed", item.Domain);
            outcome = new RegistrationOutcome(false, null, ex.Message);
        }

        if (outcome.Registered)
            return new OrderLine(item.Domain, item.Years, price, true, outcome.OrderId, outcome.Message, Refunded: false);

        // Shopper paid for something we could not deliver: give that part back.
        var refunded = false;
        try
        {
            await payments.RefundAsync(intent.Id, price, $"{intent.Id}-{item.Domain.Value}", ct);
            refunded = true;
        }
        catch (ExternalServiceException ex)
        {
            logger.LogError(ex, "Refund for {Domain} on {PaymentIntent} failed; needs manual follow-up", item.Domain, intent.Id);
        }

        return new OrderLine(item.Domain, item.Years, price, false, null, outcome.Message, refunded);
    }

    private static void ValidateContact(Contact c)
    {
        var errors = new Dictionary<string, string[]>();
        void Require(string field, string? value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)) errors[field] = [$"{label} is required."];
        }

        if (c is null) throw new ValidationException("Registrant contact details are required.");

        Require(nameof(c.FirstName), c.FirstName, "First name");
        Require(nameof(c.LastName), c.LastName, "Last name");
        Require(nameof(c.Email), c.Email, "Email");
        Require(nameof(c.Phone), c.Phone, "Phone");
        Require(nameof(c.Address1), c.Address1, "Address");
        Require(nameof(c.City), c.City, "City");
        Require(nameof(c.StateProvince), c.StateProvince, "State / province");
        Require(nameof(c.PostalCode), c.PostalCode, "Postal code");
        Require(nameof(c.Country), c.Country, "Country");

        if (!errors.ContainsKey(nameof(c.Email)) && !MailAddress.TryCreate(c.Email, out _))
            errors[nameof(c.Email)] = ["Enter a valid email address."];
        if (!errors.ContainsKey(nameof(c.Country)) && c.Country.Trim().Length != 2)
            errors[nameof(c.Country)] = ["Use the two-letter country code, e.g. US or BD."];
        if (!errors.ContainsKey(nameof(c.Phone)) && c.Phone.Count(char.IsDigit) < 7)
            errors[nameof(c.Phone)] = ["Enter a phone number with country code, e.g. +1 555 123 4567."];

        if (errors.Count > 0)
            throw new ValidationException("Please check the registrant details.", errors);
    }
}
