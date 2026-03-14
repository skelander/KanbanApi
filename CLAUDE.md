# KanbanApi

ASP.NET Core REST API for managing Kanban boards with multi-user access, JWT auth, and card state history for flow metrics.

## Tech Stack
- .NET 10
- ASP.NET Core MVC (controllers)
- EF Core + SQLite (production on Fly.io volume, SQLite :memory: in tests)
- Microsoft.AspNetCore.Authentication.JwtBearer
- BCrypt.Net-Next for password hashing
- xUnit + `WebApplicationFactory` for integration tests

## Project Structure
```
KanbanApi/
  Controllers/   AuthController, BoardsController, ColumnsController, CardsController
  Services/      IAuthService, IBoardService, IColumnService, ICardService + implementations
  Models/        User, Board, BoardMember, Column, Card, CardStateHistory, JwtSettings, Dtos.cs
  Data/          AppDbContext
  Program.cs     DI, JWT setup, CORS, EnsureCreated, admin seeding

KanbanApi.Tests/
  KanbanApiFactory.cs         SQLite :memory: + test JWT key override
  Helpers.cs                  LoginAsync, SetBearer extension
  AuthControllerTests.cs
  BoardsControllerTests.cs
  ColumnsControllerTests.cs
  CardsControllerTests.cs
```

## API Endpoints
- `POST /auth/login` — public
- `GET /auth/users` — admin only
- `POST /auth/users` — admin only
- `DELETE /auth/users/{id}` — admin only (cannot delete last admin)
- `GET /boards` — authenticated; members see their own boards, admins see all boards
- `POST /boards` — admin only, auto-creates To Do / Doing / Done columns
- `GET/PUT/DELETE /boards/{id}` — member or admin (update/delete: owner or admin)
- `GET/POST /boards/{id}/members` — member or admin (POST: owner or admin)
- `DELETE /boards/{id}/members/{userId}` — owner or admin (cannot remove owner)
- `GET /boards/{boardId}/columns` — member or admin (read-only; no create/update/delete)
- `GET/POST /boards/{boardId}/columns/{columnId}/cards` — member or admin
- `PUT/DELETE /boards/{boardId}/columns/{columnId}/cards/{cardId}` — member or admin
- `PUT /boards/{boardId}/columns/{columnId}/cards/{cardId}/move` — member or admin
- `GET /health` — public
- `POST /boards/{boardId}/testdata` — admin only; seeds multi-sprint dataset (~12 sprints, ~44 cards)
- `POST /boards/{boardId}/testdata/backlog` — admin only; seeds 15 backlog items
- `POST /boards/{boardId}/testdata/midsprint` — admin only; seeds mid-sprint snapshot (7 days into a 2-week sprint)

## Architecture
- Controllers depend on interfaces only
- `ServiceResult<T>` pattern with `Ok/NotFound/Forbidden/Conflict` statuses — services return these, controllers map to HTTP; delete/void operations use the non-generic `ServiceResult`
- WIP limit enforced on column: create card returns 409 if `Cards.Count >= WipLimit`; move card returns 409 on cross-column move if target is at limit
- Card has optional `Description` (max 500 chars); title max 100 chars
- `isAdmin` flag on all service methods — admin users bypass membership checks
- JWT: HMAC-SHA256, 8h expiry, claims: NameIdentifier (userId), Name, Role
- CORS: allows `https://skelander.github.io` and `http://localhost:5173`
- `EnsureCreated()` at startup (no migrations)
- `BoardMember` join table: composite PK (BoardId, UserId)
- Board creation wrapped in transaction: board + member + 3 default columns in one atomic operation

## Card State History
Every card has a `CardStateHistory` collection tracking column transitions:
- `EnteredAt` / `ExitedAt` (DateTime, UTC) — for cycle time / lead time
- `EnteredDate` / `ExitedDate` (DateOnly) — for throughput / CFD bucketing by day
- Created on card creation (ExitedAt = null), updated on move (close current, open new)

## Code Style
- C# primary constructors throughout
- Nullable enabled
- `ILogger<T>` injected in all services via primary constructor
- DataAnnotations on DTO records for model validation

## Configuration
| Key | Description |
|-----|-------------|
| `ConnectionStrings:DefaultConnection` | SQLite path |
| `Jwt:Key` | Signing key — empty in appsettings.json, dev key in appsettings.Development.json, production set via Fly.io secret `Jwt__Key` |
| `Jwt:Issuer` | `KanbanApi` |
| `Jwt:Audience` | `KanbanApi` |
| `RateLimit:LoginPermitLimit` | Max login requests per IP per minute (default: 10; tests override to 1000) |

## Seeded Users
| Username | Password | Role  |
|----------|----------|-------|
| admin    | admin    | admin |

Additional users created via `POST /auth/users` (admin only). Allowed roles: `user`, `admin`.

## Testing
- 47 integration tests, all using `IClassFixture<KanbanApiFactory>`
- `KanbanApiFactory`: SQLite :memory: kept-open connection, test JWT key override
- Test command: `dotnet test KanbanApi.slnx`
- dotnet at `C:\Program Files\dotnet\dotnet.exe` (not on PATH in bash — use PowerShell or full path)

## Deployment
- GitHub repo: https://github.com/skelander/KanbanApi (default branch: main)
- API deployed to: https://kanban-rikard.fly.dev
- Fly.io app: `kanban-rikard`, region: `arn`, volume: `kanban_data` at `/data/kanban.db`
- Solution file: `KanbanApi.slnx` (not `.sln`)
- CI/CD: `.github/workflows/api.yml` — triggers on `KanbanApi/**`, `KanbanApi.Tests/**`, `Dockerfile`, `fly.toml`; SDK pinned to `10.0.103` (10.0.201 does not exist on GitHub Actions CDN)
