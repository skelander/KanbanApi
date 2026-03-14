using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public interface ITestDataService
{
    Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default);
    Task<ServiceResult> SeedBacklogAsync(int boardId, CancellationToken ct = default);
    Task<ServiceResult> SeedMidSprintAsync(int boardId, CancellationToken ct = default);
}

public class TestDataService(AppDbContext db, ILogger<TestDataService> logger) : ITestDataService
{
    private record SprintCard(string Title, string? Description, int ToDoDay, int? DoingDay, int? DoneDay);

    private static readonly string[] BacklogItems =
    [
        "Dark mode support",
        "Export board as PDF",
        "Email notifications for card assignments",
        "Recurring card templates",
        "Board activity feed",
        "Attach files to cards",
        "Card due dates and reminders",
        "Sub-tasks / checklists on cards",
        "Custom labels and tags",
        "Board templates library",
        "Two-factor authentication",
        "Keyboard shortcuts",
        "Bulk move cards between columns",
        "Card comments and @mentions",
        "Time tracking per card",
    ];

    // Mid-sprint snapshot: 2-week sprint, currently at day 7.
    // Some items done early, some actively in progress, some aging in To Do.
    // Days are relative to sprint start (0 = 7 days ago).
    private static readonly SprintCard[] MidSprintCards =
    [
        new("Set up CI/CD pipeline",      "Configure GitHub Actions for build, test, and deploy.",        0,  0,  1),
        new("Database migrations",        "Define entity relationships and run initial migrations.",       0,  1,  3),
        new("User auth endpoints",        "Implement JWT login and registration endpoints.",               0,  2,  5),
        new("Login UI",                   "Build login and registration forms.",                          0,  3,  null),
        new("Write auth tests",           "Integration tests for the authentication flow.",               1,  5,  null),
        new("Fix signup validation bug",  "Signup form accepts invalid email addresses — hotfix.",        6,  6,  null),
        new("Password reset flow",        "Allow users to reset password via email link.",                0,  null, null),
        new("Dashboard layout",           "Design and implement the main dashboard UI.",                  2,  null, null),
        new("API rate limiting",          "Per-IP fixed-window rate limiting on login endpoint.",         4,  null, null),
        new("User profile page",          null,                                                           6,  null, null),
    ];

    // Multi-sprint dataset: 7 two-week sprints (~14 weeks of team history).
    // ToDoAgo/DoingAgo/DoneAgo are days before today when each transition occurred.
    // Highlights: zombie items (54d, 40d in To Do), aging WIP, and completed work.
    private record MultiSprintCard(string Title, string? Description, int ToDoAgo, int? DoingAgo, int? DoneAgo);

    private static readonly MultiSprintCard[] MultiSprintCards =
    [
        // Sprint 1 (168–154 days ago) — all done (project kickoff)
        new("Repo and tooling setup",    "Create GitHub repo, branch protection, CI skeleton, and team access.",      167, 166, 163),
        new("Dev environment",           "Standardise local setup with docker-compose, .editorconfig, and README.",   167, 164, 159),
        new("Tech stack decision",       "Evaluate and agree on backend/frontend frameworks, DB, and hosting.",       165, 161, 156),

        // Sprint 2 (154–140 days ago) — all done
        new("CI/CD pipeline",            "GitHub Actions: lint, build, test, and deploy to staging on merge.",        153, 152, 148),
        new("Staging environment",       "Provision staging server; automate deployment from main branch.",           153, 149, 144),
        new("Structured logging",        "Structured logging with correlation IDs; alert on 5xx error rate.",         151, 147, 142),

        // Sprint 3 (140–126 days ago) — all done
        new("Domain model v1",           "Finalise entity graph and EF Core migrations for the v1 schema.",           139, 138, 134),
        new("REST API skeleton",         "Controller routing, global error handler, and OpenAPI spec scaffold.",      139, 136, 130),
        new("Auth spike",                "Evaluate JWT vs session auth; document decision and security trade-offs.",  137, 133, 128),

        // Sprint 4 (126–112 days ago) — 3 done, 1 zombie in To Do
        new("User management API",       "CRUD for users; admin-only delete; password hashing with BCrypt.",          125, 124, 120),
        new("Role-based access",         "Admin and user roles; route-level authorization enforcement.",              125, 121, 116),
        new("API integration tests",     "WebApplicationFactory test suite with happy-path and error scenarios.",     124, 119, 114),
        new("Refresh token support",     "Silent re-auth with short-lived access and long-lived refresh tokens.",     123, null, null), // zombie – de-scoped

        // Sprint 5 (112–98 days ago) — 2 done, 1 in Doing, 1 in To Do
        new("Board data model",          "Board, Column, Card entities with EF Core relationships and migrations.",   111, 110, 106),
        new("Board CRUD endpoints",      "Create/read/update/delete boards with owner-only access enforcement.",      111, 107, 102),
        new("Board membership API",      "Invite and remove members; permission checks on all board endpoints.",      110, 103, null), // doing at sprint end
        new("Audit log",                 "Append-only audit trail for all write operations.",                         108, null, null), // stuck

        // Sprint 6 (98–84 days ago) — all done
        new("Initial project setup",     "Scaffold the project, configure linting, and set up local dev environment.", 97, 96, 94),
        new("Core data models",          "Define User, Board, Column, and Card entities with EF Core.",                96, 94, 91),
        new("Basic API structure",       "Set up controller routing, middleware pipeline, and error handling.",        95, 93, 88),

        // Sprint 7 (84–70 days ago) — all done
        new("User registration",         "Registration endpoint with validation and email uniqueness check.",          83, 82, 78),
        new("JWT authentication",        "Implement JWT issuance and validation with role-based claims.",              83, 80, 75),
        new("Password security",         "BCrypt hashing, salt rounds, and brute-force protection.",                  82, 78, 72),

        // Sprint 8 (70–56 days ago) — all done
        new("Board CRUD",                "Create, read, update, and delete boards with ownership checks.",            69, 68, 64),
        new("Column management",         "Reorder columns; enforce position uniqueness per board.",                   69, 66, 61),
        new("Card creation and editing", "Full card lifecycle: create, update title/description, delete.",            68, 64, 58),
        new("Drag-and-drop reordering",  "Reorder cards within and across columns via position updates.",             67, 62, 57),

        // Sprint 9 (56–42 days ago) — 3 done, 1 zombie in To Do
        new("WIP limits",                "Configurable per-column WIP limits with 409 Conflict enforcement.",         55, 54, 51),
        new("Board memberships",         "Invite users to boards; owner and member permission model.",                55, 52, 47),
        new("Card state history",        "Track column transitions with timestamps for analytics.",                   54, 50, 44),
        new("Accessibility improvements","WCAG 2.1 AA audit: fix contrast ratios and keyboard navigation.",           54, null, null), // zombie

        // Sprint 10 (42–28 days ago) — 3 done, 1 stuck in To Do
        new("Full-text search",          "Search cards and boards by keyword using EF Core LIKE queries.",            41, 40, 36),
        new("File attachments",          "Upload and serve file attachments on cards via object storage.",            41, 37, 33),
        new("Performance profiling",     "Identify and fix slow queries; add missing indexes.",                       39, 35, 30),
        new("Email notifications",       "Transactional emails for card assignments and due date reminders.",         40, null, null), // stuck

        // Sprint 11 (28–14 days ago) — 2 done, 2 in Doing, 1 in To Do
        new("Dark mode",                 "System-preference-aware dark theme using CSS custom properties.",           27, 26, 22),
        new("Mobile responsive layout",  "Ensure all views work correctly on phones and tablets.",                   27, 23, 18),
        new("User profile page",         "Avatar, display name, and notification preference settings.",              26, 19, null), // doing
        new("Board analytics dashboard", "Throughput, WIP, and cycle time summary per board.",                       25, 17, null), // doing
        new("API documentation",         "OpenAPI/Swagger spec for all endpoints with request/response examples.",   24, null, null), // to do

        // Sprint 12 (14–0 days ago, current) — 1 done, 2 in Doing, 3 in To Do
        new("Keyboard shortcuts",        "Global keyboard shortcuts for common actions (new card, move, etc.).",      13, 12, 9),
        new("Bulk card operations",      "Select multiple cards to move, label, or delete in one action.",           13, 10, null), // doing
        new("Card due dates",            "Set, display, and filter cards by due date; highlight overdue cards.",      12, 8,  null), // doing
        new("Sub-tasks / checklists",    "Nested checklist items on cards with completion tracking.",                 11, null, null),
        new("Activity feed",             "Per-board feed of all card and member activity.",                           10, null, null),
        new("Recurring card templates",  "Schedule cards to be created automatically at a set interval.",             8, null, null),
    ];

    public Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default) =>
        SeedMultiSprintAsync(boardId, ct);

    public Task<ServiceResult> SeedMidSprintAsync(int boardId, CancellationToken ct = default) =>
        SeedSprintAsync(boardId, MidSprintCards, daysAgo: 7, ct);

    private async Task<ServiceResult> SeedSprintAsync(int boardId, SprintCard[] cards, int daysAgo, CancellationToken ct)
    {
        var columns = await db.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        if (columns.Count == 0) return ServiceResult.NotFound();

        var columnIds = columns.Select(c => c.Id).ToList();
        var existing = await db.Cards.Where(c => columnIds.Contains(c.ColumnId)).ToListAsync(ct);
        db.Cards.RemoveRange(existing);

        var sprintStart = DateTime.UtcNow.AddDays(-daysAgo);
        var backlogCol = columns.First(c => c.IsBacklog);
        var workCols = columns.Where(c => !c.IsBacklog).OrderBy(c => c.Position).ToList();
        var toDoCol = workCols.Count > 0 ? workCols[0] : backlogCol;
        var lastCol = workCols.Count > 0 ? workCols[workCols.Count - 1] : backlogCol;
        var doingCol = workCols.Count > 2 ? workCols[workCols.Count / 2] : null;

        var positions = columns.ToDictionary(c => c.Id, _ => 0);

        foreach (var card in cards)
        {
            var targetCol = TargetColumn(card, toDoCol, doingCol, lastCol);
            var entity = new Card
            {
                Title = card.Title,
                Description = card.Description,
                ColumnId = targetCol.Id,
                Position = positions[targetCol.Id]++,
            };
            BuildHistory(entity, card, backlogCol, toDoCol, doingCol, lastCol, sprintStart);
            db.Cards.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} sprint cards for board {BoardId}", cards.Length, boardId);
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> SeedMultiSprintAsync(int boardId, CancellationToken ct)
    {
        var columns = await db.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        if (columns.Count == 0) return ServiceResult.NotFound();

        var columnIds = columns.Select(c => c.Id).ToList();
        var existing = await db.Cards.Where(c => columnIds.Contains(c.ColumnId)).ToListAsync(ct);
        db.Cards.RemoveRange(existing);

        var now = DateTime.UtcNow;
        var backlogCol = columns.First(c => c.IsBacklog);
        var workCols = columns.Where(c => !c.IsBacklog).OrderBy(c => c.Position).ToList();
        var toDoCol = workCols.Count > 0 ? workCols[0] : backlogCol;
        var lastCol = workCols.Count > 0 ? workCols[workCols.Count - 1] : backlogCol;
        var doingCol = workCols.Count > 2 ? workCols[workCols.Count / 2] : null;

        var positions = columns.ToDictionary(c => c.Id, _ => 0);

        foreach (var card in MultiSprintCards)
        {
            var targetCol = card.DoneAgo.HasValue ? lastCol
                : card.DoingAgo.HasValue && doingCol is not null ? doingCol
                : toDoCol;

            var entity = new Card
            {
                Title = card.Title,
                Description = card.Description,
                ColumnId = targetCol.Id,
                Position = positions[targetCol.Id]++,
            };
            BuildAbsoluteHistory(entity, card, backlogCol, toDoCol, doingCol, lastCol, now);
            db.Cards.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} multi-sprint cards for board {BoardId}", MultiSprintCards.Length, boardId);
        return ServiceResult.Ok();
    }

    private static void BuildAbsoluteHistory(
        Card entity, MultiSprintCard card,
        Column backlogCol, Column toDoCol, Column? doingCol, Column lastCol,
        DateTime now)
    {
        var toDoEntered = now.AddDays(-card.ToDoAgo);
        var backlogEntered = toDoEntered.AddDays(-1);

        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = backlogCol.Id, ColumnName = backlogCol.Name,
            EnteredAt = backlogEntered, ExitedAt = toDoEntered,
        });

        if (!card.DoingAgo.HasValue)
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = toDoCol.Id, ColumnName = toDoCol.Name,
                EnteredAt = toDoEntered,
            });
            return;
        }

        var doingEntered = now.AddDays(-card.DoingAgo.Value);
        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = toDoCol.Id, ColumnName = toDoCol.Name,
            EnteredAt = toDoEntered, ExitedAt = doingEntered,
        });

        if (!card.DoneAgo.HasValue)
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = doingCol!.Id, ColumnName = doingCol.Name,
                EnteredAt = doingEntered,
            });
            return;
        }

        var doneEntered = now.AddDays(-card.DoneAgo.Value);
        if (doingCol is not null)
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = doingCol.Id, ColumnName = doingCol.Name,
                EnteredAt = doingEntered, ExitedAt = doneEntered,
            });
        }
        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = lastCol.Id, ColumnName = lastCol.Name,
            EnteredAt = doneEntered,
        });
    }

    public async Task<ServiceResult> SeedBacklogAsync(int boardId, CancellationToken ct = default)
    {
        var columns = await db.Columns
            .Where(c => c.BoardId == boardId)
            .ToListAsync(ct);

        if (columns.Count == 0) return ServiceResult.NotFound();

        var backlogCol = columns.FirstOrDefault(c => c.IsBacklog);
        if (backlogCol is null) return ServiceResult.NotFound();

        // Clear all existing cards (and their StateHistory via cascade) before seeding
        var columnIds = columns.Select(c => c.Id).ToList();
        var existing = await db.Cards.Where(c => columnIds.Contains(c.ColumnId)).ToListAsync(ct);
        db.Cards.RemoveRange(existing);

        var now = DateTime.UtcNow;
        for (int i = 0; i < BacklogItems.Length; i++)
        {
            var entity = new Card
            {
                Title = BacklogItems[i],
                ColumnId = backlogCol.Id,
                Position = i,
            };
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = backlogCol.Id,
                ColumnName = backlogCol.Name,
                EnteredAt = now,
            });
            db.Cards.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} backlog cards for board {BoardId}", BacklogItems.Length, boardId);
        return ServiceResult.Ok();
    }

    // Cards with DoneDay go to the last column; cards with DoingDay go to the doing column
    // (if it exists); everything else stays in the To Do column.
    private static Column TargetColumn(SprintCard card, Column toDoCol, Column? doingCol, Column lastCol) =>
        card.DoneDay.HasValue ? lastCol :
        card.DoingDay.HasValue && doingCol is not null ? doingCol :
        toDoCol;

    private static void BuildHistory(
        Card entity, SprintCard card,
        Column backlogCol, Column toDoCol, Column? doingCol, Column lastCol,
        DateTime sprintStart)
    {
        var backlogEntered = sprintStart;
        var toDoEntered = sprintStart.AddDays(card.ToDoDay);

        // All cards start in backlog — exited when moved to To Do
        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = backlogCol.Id, ColumnName = backlogCol.Name,
            EnteredAt = backlogEntered, ExitedAt = toDoEntered,
        });

        // Cards with no DoingDay stay in To Do
        if (!card.DoingDay.HasValue || (doingCol is null && !card.DoneDay.HasValue))
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = toDoCol.Id, ColumnName = toDoCol.Name,
                EnteredAt = toDoEntered,
            });
            return;
        }

        var doingEntered = sprintStart.AddDays(card.DoingDay!.Value);

        // To Do → exited when work started
        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = toDoCol.Id, ColumnName = toDoCol.Name,
            EnteredAt = toDoEntered, ExitedAt = doingEntered,
        });

        if (!card.DoneDay.HasValue)
        {
            // In progress — open entry in doing column
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = doingCol!.Id, ColumnName = doingCol!.Name,
                EnteredAt = doingEntered,
            });
            return;
        }

        var doneEntered = sprintStart.AddDays(card.DoneDay!.Value);

        // Record the doing phase before marking done
        if (doingCol is not null)
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = doingCol.Id, ColumnName = doingCol.Name,
                EnteredAt = doingEntered, ExitedAt = doneEntered,
            });
        }

        // Open entry in last column (done)
        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = lastCol.Id, ColumnName = lastCol.Name,
            EnteredAt = doneEntered,
        });
    }
}
