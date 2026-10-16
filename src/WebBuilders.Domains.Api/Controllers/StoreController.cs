using Microsoft.AspNetCore.Mvc;
using WebBuilders.Domains.Api.Contracts;
using WebBuilders.Domains.Business.Services;

namespace WebBuilders.Domains.Api.Controllers;

[ApiController]
[Route("api/store")]
public sealed class StoreController(ICheckoutService checkout) : ControllerBase
{
    /// <summary>Prices, supported extensions and the Stripe publishable key for the page.</summary>
    [HttpGet("config")]
    public ActionResult<StoreConfigDto> GetConfig() => StoreConfigDto.From(checkout.GetSettings());
}
