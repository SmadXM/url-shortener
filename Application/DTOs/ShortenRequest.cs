using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    public record ShortenRequest(
        [Required, Url] string Url,
        [Range(1, 365)] int? TtlDays = null,
        bool Reuse = true
    );
}
