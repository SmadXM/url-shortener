# URL Shortener API

REST API for URL shortening with JWT authentication, Redis caching, rate limiting, and click analytics.

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
Domain/                → entities (ShortLink, User)
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

## Authentication

All link operations require a JWT Bearer token.

1. `POST /api/auth/register` — create an account
2. `POST /api/auth/login` — get a JWT token
3. Click **Authorize** in Swagger UI and paste the token

```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "secret123"
}
```

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "secret123"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

## Endpoints

| Method | URL | Auth | Description |
|--------|-----|------|-------------|
| `POST` | `/api/auth/register` | — | Register a new user |
| `POST` | `/api/auth/login` | — | Login and get JWT token |
| `POST` | `/api/links` | ✅ | Create a short link |
| `GET` | `/{code}` | — | Redirect to original URL |
| `GET` | `/api/links/{code}/stats` | ✅ owner only | Click stats for a link |
| `GET` | `/api/links/myLinks` | ✅ | All links for current user |

### Create a short link

```http
POST /api/links
Authorization: Bearer <token>
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
Authorization: Bearer <token>
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

**JWT Authentication** — register and login to get a Bearer token. All link operations require authentication. Stats are accessible to the owner only, returning 403 for unauthorized access.

**URL reuse** — if the same URL is shortened again with `reuse: true` (default), the existing active link is returned instead of creating a duplicate. Reuse is scoped per user — each user gets their own short code for the same URL.

**TTL support** — links can have an expiration date (1–365 days). Expired links return 404.

**Rate limiting** — 10 requests per minute per IP on the shorten endpoint. Returns `429 Too Many Requests` when exceeded.

**Redis caching** — resolved links are cached for 10 minutes. Cache is invalidated on every click increment. URL-to-code mapping is also cached per user for 60 minutes to speed up reuse lookups.

## Running Tests

```bash
dotnet test
```

15+ unit tests covering: code generation, uniqueness, TTL expiry, cache hits, invalid URLs, reuse per user, expired links, deleted links, and edge cases.

## Local Development (without Docker)

1. Run PostgreSQL on port `5433` and Redis on `6379`
2. Connection strings are in `API/appsettings.Development.json`
3. Set JWT env variables in `launchSettings.json` under `environmentVariables`
4. Apply DB schema manually or via EF migrations
5. `dotnet run --project API`