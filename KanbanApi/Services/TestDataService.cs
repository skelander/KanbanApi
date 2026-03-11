using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public interface ITestDataService
{
    Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default);
    Task<ServiceResult> SeedBacklogAsync(int boardId, CancellationToken ct = default);
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

    // A simulated two-week sprint for a software project.
    // Days are relative to sprint start (0 = 14 days ago).
    private static readonly SprintCard[] Cards =
    [
        new("Database schema design",        "Define entity relationships and index strategy.",              0,  1,  3),
        new("Set up CI/CD pipeline",         "Configure GitHub Actions for build, test, and deploy.",       0,  2,  5),
        new("User authentication",           "Implement JWT login flow with role-based access control.",    1,  3,  7),
        new("API rate limiting",             "Per-IP fixed-window rate limiting on sensitive endpoints.",   3,  6, 10),
        new("Implement search feature",      "Full-text search across cards and board names.",              6,  9, null),
        new("Fix mobile login bug",          "Login form fails to submit on iOS Safari — investigate.",     8, 10, null),
        new("Write integration tests",       "Cover auth, boards, columns, and cards endpoints.",           9, 11, null),
        new("Add pagination to endpoints",   null,                                                          5, null, null),
        new("Performance optimization",      "Investigate slow board load on large datasets.",              6, null, null),
        new("Update API documentation",      null,                                                          7, null, null),
        new("Code review: auth module",      null,                                                          8, null, null),
        new("Refactor database queries",     "Eliminate N+1 queries in board and card loading.",            9, null, null),
    ];

    public async Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default)
    {
        var columns = await db.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        if (columns.Count == 0) return ServiceResult.NotFound();

        // Clear all existing cards (and their StateHistory via cascade) before seeding
        var columnIds = columns.Select(c => c.Id).ToList();
        var existing = await db.Cards.Where(c => columnIds.Contains(c.ColumnId)).ToListAsync(ct);
        db.Cards.RemoveRange(existing);

        var sprintStart = DateTime.UtcNow.AddDays(-14);
        var backlogCol = columns.First(c => c.IsBacklog);
        var workCols = columns.Where(c => !c.IsBacklog).OrderBy(c => c.Position).ToList();
        var toDoCol = workCols.Count > 0 ? workCols[0] : backlogCol;
        var lastCol = workCols.Count > 0 ? workCols[workCols.Count - 1] : backlogCol;
        var doingCol = workCols.Count > 2 ? workCols[workCols.Count / 2] : null;

        // All positions start at 0 after the clear
        var positions = columns.ToDictionary(c => c.Id, _ => 0);

        foreach (var card in Cards)
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
        logger.LogInformation("Seeded {Count} test cards for board {BoardId}", Cards.Length, boardId);
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
