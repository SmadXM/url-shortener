# URL Shortener API

REST API for URL shortening with Redis caching, rate limiting, and click analytics.

## Tech Stack

- **ASP.NET Core 10** — Web API
- **PostgreSQL 16** — persistent storage
- **Redis 7** — Cache-Aside pattern, 10-minute TTL
- **Docker Compose** — one-command setup
- **xUnit + Moq** — unit tests

## Architecture

```
API/                   → controllers, middleware, program entry
Application/           → business logic, services, DTOs
Domain/                → entities (ShortLink)
Infrastructure/        → EF Core, AppDbContext
Tests/                 → unit tests
```

Dependencies flow: `API → Application → Domain ← Infrastructure`

## Quick Start

**Prerequisites:** Docker Desktop

```bash
git clone https://github.com/SmadXM/url-shortener.git
cd url-shortener
docker compose up --build
```

API available at: `http://localhost:5000/swagger`

## Endpoints

| Method | URL | Description |
|--------|-----|-------------|
| `POST` | `/api/links` | Create a short link |
| `GET` | `/{code}` | Redirect to original URL |
| `GET` | `/api/links/{code}/stats` | Click stats for a link |

### Create a short link

```http
POST /api/links
Content-Type: application/json

{
  "url": "https://example.com",
  "ttlDays": 30,
  "reuse": true
}
```

**Response:**
```json
{
  "shortUrl": "http://localhost:5000/aB3xYz",
  "code": "aB3xYz",
  "createdAt": "2026-05-17T10:00:00Z",
  "expiresAt": "2026-06-16T10:00:00Z"
}
```

### Get stats

```http
GET /api/links/aB3xYz/stats
```

```json
{
  "globalId": "...",
  "originalUrl": "https://example.com",
  "code": "aB3xYz",
  "clickCount": 42,
  "createdAt": "2026-05-17T10:00:00Z",
  "expiresAt": "2026-06-16T10:00:00Z"
}
```

## Features

**URL reuse** — if the same URL is shortened again with `reuse: true` (default), the existing active link is returned instead of creating a duplicate.

**TTL support** — links can have an expiration date (1–365 days). Expired links return 404.

**Rate limiting** — 10 requests per minute per IP on the shorten endpoint. Returns `429 Too Many Requests` when exceeded.

**Redis caching** — resolved links are cached for 10 minutes. Cache is invalidated on every click increment.

## Running Tests

```bash
dotnet test
```

5 unit tests covering: code generation, uniqueness, TTL expiry, non-existent codes, reuse behaviour.

## Local Development (without Docker)

1. Run PostgreSQL on port `5433` and Redis on `6379`
2. Connection strings are in `API/appsettings.Development.json`
3. Apply DB schema manually or via EF migrations
4. `dotnet run --project API`