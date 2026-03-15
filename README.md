# KanbanApi

A REST API for managing Kanban boards, built with ASP.NET Core and SQLite. Supports multi-user boards, JWT authentication, and card state history for flow metrics.

## Tech Stack

- .NET 10 / ASP.NET Core (controllers)
- EF Core + SQLite
- JWT Bearer authentication (HMAC-SHA256)
- BCrypt.Net-Next for password hashing
- xUnit + `WebApplicationFactory` for integration tests
- Deployed on [Fly.io](https://fly.io)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [flyctl](https://fly.io/docs/hands-on/install-flyctl/) (for deployment only)

## Local Setup

```bash
git clone https://github.com/skelander/KanbanApi.git
cd KanbanApi
dotnet build KanbanApi.slnx
dotnet run --project KanbanApi
```

The API starts on `http://localhost:5154`. A default `admin/admin` user is seeded on first run.

The `appsettings.Development.json` (included in the repo) provides a local JWT key. For production, set the `Jwt__Key` environment variable (or Fly.io secret).

## Running Tests

```bash
dotnet test KanbanApi.slnx
```

47 integration tests using an in-memory SQLite database. No external dependencies required.

## API Endpoints

### Authentication

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/auth/login` | — | Login, returns JWT token |
| GET | `/auth/users` | admin | List all users |
| POST | `/auth/users` | admin | Create user |
| DELETE | `/auth/users/{id}` | admin | Delete user |

### Boards

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/boards` | user/admin | List boards (members see own boards; admin sees all) |
| POST | `/boards` | admin | Create board (auto-creates Todo / Reqs / Code / Test / Done columns) |
| GET | `/boards/{id}` | member/admin | Get board with columns and members |
| PUT | `/boards/{id}` | owner/admin | Update board name or description |
| DELETE | `/boards/{id}` | owner/admin | Delete board |
| GET | `/boards/{id}/members` | member/admin | List board members |
| POST | `/boards/{id}/members` | owner/admin | Add member to board |
| DELETE | `/boards/{id}/members/{userId}` | owner/admin | Remove member from board |

### Columns

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/boards/{boardId}/columns` | member/admin | List columns sorted by position |

### Cards

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/boards/{boardId}/columns/{columnId}/cards` | member/admin | List cards sorted by position |
| POST | `/boards/{boardId}/columns/{columnId}/cards` | member/admin | Create card |
| PUT | `/boards/{boardId}/columns/{columnId}/cards/{cardId}` | member/admin | Update card |
| DELETE | `/boards/{boardId}/columns/{columnId}/cards/{cardId}` | member/admin | Delete card |
| PUT | `/boards/{boardId}/columns/{columnId}/cards/{cardId}/move` | member/admin | Move card to another column |

### Other

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/health` | — | Health check |
| POST | `/boards/{id}/testdata` | admin | Seed multi-sprint dataset (~12 sprints of history) |
| POST | `/boards/{id}/testdata/backlog` | admin | Seed backlog with 15 items |
| POST | `/boards/{id}/testdata/midsprint` | admin | Seed a mid-sprint snapshot (7 days into a 2-week sprint) |

### Authentication

All protected endpoints require a `Bearer` token in the `Authorization` header:

```
Authorization: Bearer <token>
```

Obtain a token via `POST /auth/login`. Tokens expire after 8 hours.

**Roles:**
- `user` — can access boards they are a member of
- `admin` — can access all boards and manage users

## Data Model

```
User ──< BoardMember >── Board ──< Column ──< Card ──< CardStateHistory
```

- **Board**: has an owner (User) and multiple members (via BoardMember join table)
- **Column**: belongs to a Board, has an optional WIP limit (enforced — create/move returns 409 when at limit)
- **Card**: belongs to a Column, has a title (max 100 chars), optional description (max 500 chars), and tracks position
- **CardStateHistory**: records EnteredAt/ExitedAt timestamps for each column a card passes through — foundation for cycle time, lead time, and CFD metrics

## Configuration

| Key | Description | Default |
|-----|-------------|---------|
| `ConnectionStrings:DefaultConnection` | SQLite connection string | `Data Source=kanban.db` |
| `Jwt:Key` | HMAC-SHA256 signing key (min 32 chars) | *(set via env var in production)* |
| `Jwt:Issuer` | JWT issuer | `KanbanApi` |
| `Jwt:Audience` | JWT audience | `KanbanApi` |
| `RateLimit:LoginPermitLimit` | Max login requests per IP per minute | `10` |

In production, set `Jwt__Key` as a Fly.io secret:
```bash
flyctl secrets set Jwt__Key=<your-random-key>
```

## Deployment

The API is deployed to Fly.io. CI/CD runs automatically on push to `main` via GitHub Actions: build → test → deploy.

```bash
# Manual deploy
flyctl deploy --remote-only
```

**Production URL:** https://kanban-rikard.fly.dev

The SQLite database is stored on a persistent Fly.io volume (`kanban_data`) mounted at `/data/kanban.db`.
