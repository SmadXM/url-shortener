namespace Application.DTOs
{
    public record ShortenResponse(
        string ShortUrl,
        string Code,
        DateTime CreatedAt,
        DateTime? ExpiresAt
    );
}
