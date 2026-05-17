using Domain.Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Application.Services
{
    public class UrlShortenerService(AppDbContext dbContext, IDistributedCache cache)
    {
        private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public async Task<ShortLink?> GetByCodeAsync(string code) => await dbContext.ShortLinks.FirstOrDefaultAsync(x => x.Code == code);

        public async Task<ShortLink> ShortenAsync(string originalUrl, int? ttlDays = null, bool reuse = true, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(originalUrl))
                throw new ArgumentException("URL cannot be empty", nameof(originalUrl));

            if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("Only http/https URLs are supported", nameof(originalUrl));

            if (reuse)
            {
                var cacheKey = $"url:{originalUrl}";
                var cachedBytes = await cache.GetAsync(cacheKey, cancellationToken);
                if (cachedBytes != null)
                {
                    var cachedCode = Encoding.UTF8.GetString(cachedBytes);
                    var cachedLink = await dbContext.ShortLinks
                        .FirstOrDefaultAsync(x => x.Code == cachedCode, cancellationToken);
                    if (cachedLink != null)
                        return cachedLink;
                }

                var existing = await dbContext.ShortLinks
                    .FirstOrDefaultAsync(x => x.OriginalUrl == originalUrl &&
                        (x.ExpiresAt > DateTime.UtcNow || !x.ExpiresAt.HasValue), cancellationToken);
                if (existing != null)
                {
                    await cache.SetAsync(cacheKey, Encoding.UTF8.GetBytes(existing.Code),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
                        }, cancellationToken);
                    return existing;
                }
            }

            string code;
            do
            {
                code = GenerateCode();
            }
            while (await dbContext.ShortLinks.AnyAsync(x => x.Code == code, cancellationToken));

            var link = new ShortLink
            {
                OriginalUrl = originalUrl,
                Code = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = ttlDays.HasValue && ttlDays.Value > 0
                    ? DateTime.UtcNow.AddDays(ttlDays.Value)
                    : null
            };

            dbContext.ShortLinks.Add(link);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (reuse)
            {
                await cache.SetAsync($"url:{originalUrl}", Encoding.UTF8.GetBytes(code),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = link.ExpiresAt.HasValue
                            ? link.ExpiresAt.Value - DateTime.UtcNow   
                            : TimeSpan.FromMinutes(60)
                    }, cancellationToken);
            }

            return link;
        }

        public async Task<ShortLink?> ResolveAsync (string code)
        {
            var cached = await cache.GetStringAsync($"link:{code}");
            if (cached != null && cached.Length > 0)
                return JsonSerializer.Deserialize<ShortLink>(cached);

            var link = await dbContext.ShortLinks
                .FirstOrDefaultAsync(x => x.Code == code && (x.ExpiresAt > DateTime.UtcNow || !x.ExpiresAt.HasValue));

            if (link == null) 
                return null;

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };

            await cache.SetStringAsync($"link:{code}",
                JsonSerializer.Serialize(link), options);

            return link;
        }

        public async Task IncrementClickAsync(string code)
        {
            await dbContext.ShortLinks
                .Where(x => x.Code == code)
                .ExecuteUpdateAsync(x => x.SetProperty(link => link.ClickCount, link => link.ClickCount + 1));

            await cache.RemoveAsync($"link:{code}");
        }

        private static string GenerateCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            return new string(bytes.Select(x => _chars[x % _chars.Length]).ToArray());
        }
    }
}
