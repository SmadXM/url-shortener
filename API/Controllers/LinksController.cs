using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

[ApiController]
[Route("api/links")]
public class LinksController(UrlShortenerService svc) : ControllerBase
{
    /// <summary>Create short link</summary>
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("shorten_limit")]
    public async Task<IActionResult> Shorten([FromBody] ShortenRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var link = await svc.ShortenAsync(request.Url, userId, request.TtlDays, request.Reuse, cancellationToken);

        var response = new ShortenResponse(
            ShortUrl: $"{Request.Scheme}://{Request.Host}/{link.Code}",
            Code: link.Code,
            CreatedAt: link.CreatedAt,
            ExpiresAt: link.ExpiresAt
        );

        return CreatedAtAction(nameof(GetStats), new { code = link.Code }, response);
    }

    /// <summary>Link's statistics</summary>
    [HttpGet("{code}/stats")]
    [Authorize]
    public async Task<IActionResult> GetStats(string code)
    {
        var userId = GetUserId();
        var link = await svc.GetByCodeAsync(code, userId);
        return link is null ? NotFound() : Ok(link);
    }

    /// <summary>User's links</summary>
    [HttpGet("myLinks")]
    [Authorize]
    public async Task<IActionResult> MyLinks()
    {
        var userId = GetUserId();
        var links = await svc.GetUserLinksAsync(userId);
        return Ok(links);
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}