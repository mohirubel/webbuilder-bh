using Microsoft.AspNetCore.Mvc;
using WebBuilders.Domains.Api.Contracts;
using WebBuilders.Domains.Business.Services;

namespace WebBuilders.Domains.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(IOrderService orders) : ControllerBase
{
    /// <summary>Finalises an order once the card payment succeeded: registers the domains, refunds any that fail.</summary>
    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> Complete([FromBody] CompleteOrderRequestDto request, CancellationToken ct) =>
        OrderResponseDto.From(await orders.CompleteOrderAsync(request.PaymentIntentId ?? "", request.Contact?.ToModel()!, ct));
}
