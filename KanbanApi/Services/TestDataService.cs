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

    // Multi-sprint dataset: 5 two-week sprints (~10 weeks of team history).
    // TodoAgo/DoingAgo/DoneAgo are days before today when each transition occurred.
    // DoingColOffset: -1=Reqs, 0=Code (default), +1=Test
    // Sprints 1–4: all cards done by sprint end. Sprint 5 (current): work in progress.
    // Seeding always recreates the work columns to match MultiSprintColumns.
    private record MultiSprintCard(string Title, string? Description, int TodoAgo, int? DoingAgo, int? DoneAgo, int DoingColOffset = 0);

    private static readonly string[] MultiSprintColumns = ["Todo", "Reqs", "Code", "Test", "Done"];

    private static readonly MultiSprintCard[] MultiSprintCards =
    [
        // Sprint 1 (70–56 days ago) — all done
        new("Repo setup and CI/CD",      "GitHub repo, branch protection, Actions pipeline, and team access.",        69, 68, 65),
        new("Domain model and schema",   "Define entities and EF Core relationships; run initial migration.",          69, 66, 61),
        new("JWT authentication",        "HMAC-SHA256 token issuance and validation with role-based claims.",          68, 64, 59),
        new("Core REST endpoints",       "Controller routing, global error handler, and health check endpoint.",       67, 62, 57),

        // Sprint 2 (56–42 days ago) — all done
        new("Board CRUD",                "Create, read, update, and delete boards with owner-only enforcement.",       55, 54, 51),
        new("Card management",           "Full card lifecycle: create, edit title/description, delete.",               55, 52, 47, DoingColOffset: 1),
        new("Column ordering",           "Enforce position uniqueness per board; reorder via position updates.",        54, 50, 45),
        new("Member access control",     "Invite and remove members; admin and owner permission model.",               53, 48, 43, DoingColOffset: -1),

        // Sprint 3 (42–28 days ago) — all done
        new("Drag-and-drop reordering",  "Reorder cards within and across columns via position updates.",              41, 40, 37),
        new("WIP limits",                "Configurable per-column WIP limits; 409 Conflict when at limit.",            41, 38, 33, DoingColOffset: 1),
        new("Card state history",        "Track column transitions with EnteredAt/ExitedAt for flow metrics.",         40, 36, 31),
        new("Integration test suite",    "WebApplicationFactory suite covering happy-path and error scenarios.",        39, 34, 29, DoingColOffset: -1),

        // Sprint 4 (28–14 days ago) — all done
        new("Work item age chart",       "SVG scatter plot: X = column, Y = days since leaving Backlog.",              27, 26, 23),
        new("Sprint navigation",         "Step through historical sprint snapshots via board state reconstruction.",   27, 24, 19, DoingColOffset: 1),
        new("Flow metrics foundation",   "85th-percentile SLE line; backlog excluded from chart.",                     26, 22, 17),
        new("Test data seeding",         "Multi-sprint seed endpoint that recreates columns dynamically.",             25, 20, 15, DoingColOffset: -1),

        // Sprint 5 (14–0 days ago, current) — 1 done, 3 in Doing (Reqs/Code/Test), 2 in Todo
        new("Dark mode",                 "System-preference-aware dark theme using CSS custom properties.",            13, 12, 9),
        new("Keyboard shortcuts",        "Global shortcuts for common actions: new card, move, delete.",               12, 8,  null, DoingColOffset: -1), // Reqs
        new("Sub-tasks / checklists",    "Nested checklist items on cards with per-item completion tracking.",         11, 7,  null),                     // Code
        new("Card due dates",            "Set, display, and filter by due date; highlight overdue cards.",             10, 5,  null, DoingColOffset: 1),  // Test
        new("Activity feed",             "Per-board feed of all card and member activity.",                            9,  null, null),
        new("Bulk card operations",      "Select multiple cards to move, label, or delete in one action.",             8,  null, null),
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

        // Remove all existing cards
        var columnIds = columns.Select(c => c.Id).ToList();
        var existing = await db.Cards.Where(c => columnIds.Contains(c.ColumnId)).ToListAsync(ct);
        db.Cards.RemoveRange(existing);

        // Replace non-backlog columns with the expected set
        var backlogCol = columns.First(c => c.IsBacklog);
        db.Columns.RemoveRange(columns.Where(c => !c.IsBacklog));

        var workCols = new List<Column>();
        for (int i = 0; i < MultiSprintColumns.Length; i++)
        {
            var col = new Column { BoardId = boardId, Name = MultiSprintColumns[i], Position = i + 1 };
            db.Columns.Add(col);
            workCols.Add(col);
        }

        await db.SaveChangesAsync(ct); // flush so new columns get their IDs

        var now = DateTime.UtcNow;
        var toDoCol = workCols[0];
        var lastCol = workCols[workCols.Count - 1];
        var midIdx = workCols.Count / 2;
        var positions = workCols.ToDictionary(c => c.Id, _ => 0);
        positions[backlogCol.Id] = 0;

        foreach (var card in MultiSprintCards)
        {
            Column? cardDoingCol = null;
            if (card.DoingAgo.HasValue)
            {
                var colIdx = Math.Clamp(midIdx + card.DoingColOffset, 1, workCols.Count - 2);
                cardDoingCol = workCols[colIdx];
            }

            var targetCol = card.DoneAgo.HasValue ? lastCol
                : cardDoingCol is not null ? cardDoingCol
                : toDoCol;

            var entity = new Card
            {
                Title = card.Title,
                Description = card.Description,
                ColumnId = targetCol.Id,
                Position = positions[targetCol.Id]++,
            };
            BuildAbsoluteHistory(entity, card, backlogCol, toDoCol, cardDoingCol, lastCol, now);
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
        var toDoEntered = now.AddDays(-card.TodoAgo);
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
