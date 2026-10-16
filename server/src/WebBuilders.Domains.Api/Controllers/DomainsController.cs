using Microsoft.AspNetCore.Mvc;
using WebBuilders.Domains.Api.Contracts;
using WebBuilders.Domains.Business.Services;

namespace WebBuilders.Domains.Api.Controllers;

[ApiController]
[Route("api/domains")]
public sealed class DomainsController(IDomainSearchService search) : ControllerBase
{
    /// <summary>Checks a name across every supported extension, e.g. <c>GET /api/domains/search?q=mybrand.io</c>.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResponseDto>> Search([FromQuery] string q, CancellationToken ct) =>
        SearchResponseDto.From(await search.SearchAsync(q, ct));
}
