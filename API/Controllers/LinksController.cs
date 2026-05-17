using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/links")]
public class LinksController(UrlShortenerService svc) : ControllerBase
{
    /// <summary>Создать короткую ссылку</summary>
    [HttpPost]
    [EnableRateLimiting("shorten_limit")]
    public async Task<IActionResult> Shorten([FromBody] ShortenRequest req)
    {
        var link = await svc.ShortenAsync(req.Url, req.TtlDays);

        var response = new ShortenResponse(
            ShortUrl: $"{Request.Scheme}://{Request.Host}/{link.Code}",
            Code: link.Code,
            CreatedAt: link.CreatedAt,
            ExpiresAt: link.ExpiresAt
        );

        return CreatedAtAction(nameof(GetStats), new { code = link.Code }, response);
    }

    /// <summary>Статистика по ссылке</summary>
    [HttpGet("{code}/stats")]
    public async Task<IActionResult> GetStats(string code)
    {
        var link = await svc.GetByCodeAsync(code);
        return link is null ? NotFound() : Ok(link);
    }
}