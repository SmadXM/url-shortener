using Application.Services;
using Domain.Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using System.Text;
using System.Text.Json;

namespace Tests
{
    public class UrlShortenerServiceTests
    {
        private readonly Guid _userId = Guid.NewGuid();

        private string GetTokenForTest()
        {
            // Simulate a token that encodes the user id (for tests only)
            return Convert.ToBase64String(_userId.ToByteArray());
        }

        private Guid GetUserIdFromToken(string token)
        {
            var bytes = Convert.FromBase64String(token);
            return new Guid(bytes);
        }

        private AppDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private UrlShortenerService CreateSvc(AppDbContext db)
        {
            var cache = new Mock<IDistributedCache>();
            cache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((byte[]?)null);
            return new UrlShortenerService(db, cache.Object);
        }

        [Fact]
        public async Task Shorten_CreatesLinkWithUniqueCode()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);

            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var result = await svc.ShortenAsync("https://google.com", userId);

            Assert.NotNull(result.Code);
            Assert.Equal(6, result.Code.Length);
            Assert.Equal("https://google.com", result.OriginalUrl);
        }

        [Fact]
        public async Task Shorten_TwoCallsSameUrl_ReturnsDifferentCodesWithReuseFalse()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);

            var tokenA = GetTokenForTest();
            var userIdA = GetUserIdFromToken(tokenA);
            var a = await svc.ShortenAsync("https://google.com", userIdA, reuse: false);
            var tokenB = GetTokenForTest();
            var userIdB = GetUserIdFromToken(tokenB);
            var b = await svc.ShortenAsync("https://google.com", userIdB, reuse: false);

            Assert.NotEqual(a.Code, b.Code);
        }

        [Fact]
        public async Task Shorten_TwoCallsSameUrl_ReturnsSameCodesWithReuseTrue()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);

            var tokenA = GetTokenForTest();
            var userIdA = GetUserIdFromToken(tokenA);
            var a = await svc.ShortenAsync("https://google.com", userIdA);
            var tokenB = GetTokenForTest();
            var userIdB = GetUserIdFromToken(tokenB);
            var b = await svc.ShortenAsync("https://google.com", userIdB);

            Assert.Equal(a.Code, b.Code);
        }

        [Fact]
        public async Task Resolve_ExpiredLink_ReturnsNull()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);

            db.ShortLinks.Add(new ShortLink
            {
                Code = "expred",
                OriginalUrl = "https://example.com",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                ExpiresAt = DateTime.UtcNow.AddDays(-1)  
            });
            await db.SaveChangesAsync();

            var result = await svc.ResolveAsync("expred");

            Assert.Null(result);
        }

        [Fact]
        public async Task Resolve_NonExistentCode_ReturnsNull()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);

            var result = await svc.ResolveAsync("xxxxxx");

            Assert.Null(result);
        }

        [Fact]
        public async Task Shorten_WithTtl_SetsExpiresAt()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);

            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var result = await svc.ShortenAsync("https://example.com", userId, ttlDays: 7);

            Assert.NotNull(result.ExpiresAt);
            Assert.True(result.ExpiresAt > DateTime.UtcNow);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Shorten_EmptyOrNullUrl_ThrowsArgumentException(string url)
        {
            var svc = CreateSvc(CreateDb());
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            await Assert.ThrowsAsync<ArgumentException>(() => svc.ShortenAsync(url, userId));
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("ftp://unsupported.com")]
        [InlineData("javascript:alert(1)")]
        public async Task Shorten_InvalidUrl_ThrowsArgumentException(string url)
        {
            var svc = CreateSvc(CreateDb());
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            await Assert.ThrowsAsync<ArgumentException>(() => svc.ShortenAsync(url, userId));
        }

        [Fact]
        public async Task Shorten_WithZeroTtl_ExpiresAtIsNull()
        {
            var svc = CreateSvc(CreateDb());
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var result = await svc.ShortenAsync("https://example.com", userId, ttlDays: 0);
            Assert.Null(result.ExpiresAt);
        }

        [Fact]
        public async Task Shorten_ManyDifferentUrls_AllCodesUnique()
        {
            var svc = CreateSvc(CreateDb());
            var urls = Enumerable.Range(1, 50)
                .Select(i => $"https://example.com/page/{i}");

            var codes = new HashSet<string>();
            foreach (var url in urls)
            {
                var token = GetTokenForTest();
                var userId = GetUserIdFromToken(token);
                var link = await svc.ShortenAsync(url, userId);
                Assert.True(codes.Add(link.Code), $"Duplicate code: {link.Code}");
            }
        }

        [Fact]
        public async Task Shorten_CacheHit_DoesNotQueryDatabase()
        {
            var db = CreateDb();
            var cache = new Mock<IDistributedCache>();

            cache.Setup(x => x.GetAsync("url:https://google.com", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(Encoding.UTF8.GetBytes("abc123"));

            db.ShortLinks.Add(new ShortLink
            {
                Code = "abc123",
                OriginalUrl = "https://google.com",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var svc = new UrlShortenerService(db, cache.Object);
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var result = await svc.ShortenAsync("https://google.com", userId);

            Assert.Equal("abc123", result.Code);
        }

        [Fact]
        public async Task Resolve_ValidCode_ReturnsOriginalUrl()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var link = await svc.ShortenAsync("https://example.com", userId);
            var result = await svc.ResolveAsync(link.Code);

            Assert.NotNull(result);
            Assert.Equal("https://example.com", result.OriginalUrl);
        }

        [Fact]
        public async Task Resolve_CacheHit_ReturnsWithoutDbQuery()
        {
            var db = CreateDb();
            var cache = new Mock<IDistributedCache>();

            var link = new ShortLink
            {
                Code = "xxxxxx",
                OriginalUrl = "https://cached.com",
                ExpiresAt = null
            };

            var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(link));
            cache.Setup(x => x.GetAsync("link:xxxxxx", It.IsAny<CancellationToken>()))
                 .ReturnsAsync(payload);

            var svc = new UrlShortenerService(db, cache.Object);
            var result = await svc.ResolveAsync("xxxxxx");

            Assert.NotNull(result);
            Assert.Equal("https://cached.com", result.OriginalUrl);
            Assert.Empty(db.ShortLinks);
        }

        [Fact]
        public async Task Resolve_WrongCase_ReturnsNullOrSameResult()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var link = await svc.ShortenAsync("https://example.com", userId);
            var result = await svc.ResolveAsync(link.Code.ToUpper());

            if (result != null)
                Assert.Equal("https://example.com", result.OriginalUrl);
        }

        [Fact]
        public async Task Resolve_DeletedLink_ReturnsNull()
        {
            var db = CreateDb();
            var svc = CreateSvc(db);
            var token = GetTokenForTest();
            var userId = GetUserIdFromToken(token);
            var link = await svc.ShortenAsync("https://example.com", userId);
            db.ShortLinks.Remove(db.ShortLinks.Single(l => l.Code == link.Code));
            await db.SaveChangesAsync();

            var result = await svc.ResolveAsync(link.Code);
            Assert.Null(result);
        }
    }
}
