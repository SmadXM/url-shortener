using Application.DTOs;
using Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(AuthService svc) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] AuthRequest req)
        {
            try
            {
                var user = await svc.RegisterUser(req.Email, req.Password);
                return Ok(new { user.GlobalId, user.Email, user.CreatedAt });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] AuthRequest req)
        {
            var token = await svc.AuthenticateUser(req.Email, req.Password);
            if (token == null)
                return Unauthorized("Invalid credentials");

            return Ok(new { token });
        }
    }
}

