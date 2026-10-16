using Microsoft.AspNetCore.Mvc;
using WebBuilders.Domains.Api.Contracts;
using WebBuilders.Domains.Business.Services;

namespace WebBuilders.Domains.Api.Controllers;

[ApiController]
[Route("api/checkout")]
public sealed class CheckoutController(ICheckoutService checkout) : ControllerBase
{
    /// <summary>Prices the cart on the server and returns a Stripe client secret for the card form.</summary>
    [HttpPost("payment-intents")]
    public async Task<ActionResult<CheckoutResponseDto>> CreatePaymentIntent([FromBody] CheckoutRequestDto request, CancellationToken ct)
    {
        var items = (request.Items ?? []).Select(i => (i.Domain ?? "", i.Years)).ToList();
        return CheckoutResponseDto.From(await checkout.StartCheckoutAsync(items, ct));
    }
}
