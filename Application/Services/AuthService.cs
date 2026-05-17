using Domain.Entities;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Application.Services
{
    public class AuthService(AppDbContext dbContext, IConfiguration configuration)
    {
        public async Task<User> RegisterUser(string email, string password)
        {
            if (await dbContext.Users.AnyAsync(x => x.Email == email))
                throw new InvalidOperationException("User with this email already exists");

            var user = new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            return user;
        }

        public async Task<string?> AuthenticateUser(string email, string password)
        {
            var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return null;

            return GenerateToken(user);
        }

        private string GenerateToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.GlobalId.ToString()),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(
                    int.Parse(configuration["Jwt:ExpiryDays"]!)),
                signingCredentials: new SigningCredentials(
                    key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
