using System.ComponentModel.DataAnnotations;

namespace Application.DTOs
{
    public record AuthRequest(
        [Required, EmailAddress] string Email,
        [Required, MinLength(6)] string Password
    );
}
