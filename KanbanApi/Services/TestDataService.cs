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

    // A simulated two-week sprint for a software project.
    // Days are relative to sprint start (0 = 14 days ago).
    private static readonly SprintCard[] Cards =
    [
        // Done
        new("Database schema design",        "Define entity relationships and index strategy.",              0,  1,  3),
        new("Set up CI/CD pipeline",         "Configure GitHub Actions for build, test, and deploy.",       0,  2,  5),
        new("Design system components",      "Button, input, card, and modal base components.",             0,  2,  4),
        new("User authentication",           "Implement JWT login flow with role-based access control.",    1,  3,  7),
        new("User roles and permissions",    "Admin, member, and viewer role enforcement on all routes.",   1,  3,  8),
        new("API rate limiting",             "Per-IP fixed-window rate limiting on sensitive endpoints.",   3,  6, 10),
        new("Board management UI",           "Create, rename, archive, and delete boards.",                 2,  5, 11),
        new("Responsive layout",             "Ensure the app works on mobile and tablet screen sizes.",     4,  8, 12),
        new("Logging and error handling",    "Structured logging with correlation IDs; global error page.", 2,  6, 13),
        // Doing
        new("Implement search feature",      "Full-text search across cards and board names.",              6,  9, null),
        new("Fix mobile login bug",          "Login form fails to submit on iOS Safari — investigate.",     8, 10, null),
        new("Write integration tests",       "Cover auth, boards, columns, and cards endpoints.",           9, 11, null),
        new("File upload service",           "Allow attachments on cards; store in object storage.",        3,  9, null),
        new("Email notification service",    "Notify members on card assignment and due date changes.",     5, 12, null),
        // To Do
        new("Add pagination to endpoints",   null,                                                          5, null, null),
        new("Performance optimization",      "Investigate slow board load on large datasets.",              6, null, null),
        new("Update API documentation",      null,                                                          7, null, null),
        new("Code review: auth module",      null,                                                          8, null, null),
        new("Refactor database queries",     "Eliminate N+1 queries in board and card loading.",            9, null, null),
        new("Accessibility audit",           "WCAG 2.1 AA compliance check; fix critical violations.",     10, null, null),
    ];

    public Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default) =>
        SeedSprintAsync(boardId, Cards, daysAgo: 14, ct);

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
