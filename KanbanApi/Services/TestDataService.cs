using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public interface ITestDataService
{
    Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default);
}

public class TestDataService(AppDbContext db, ILogger<TestDataService> logger) : ITestDataService
{
    private record SprintCard(string Title, string? Description, int ToDoDay, int? DoingDay, int? DoneDay);

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

        var sprintStart = DateTime.UtcNow.AddDays(-14);
        var firstCol = columns.First();
        var lastCol = columns.Last();
        var midCol = columns.Count > 2 ? columns[columns.Count / 2] : null;

        // Pre-load current max positions to avoid per-card DB queries
        var positions = new Dictionary<int, int>();
        foreach (var col in columns)
        {
            var max = await db.Cards
                .Where(c => c.ColumnId == col.Id)
                .MaxAsync(c => (int?)c.Position, ct);
            positions[col.Id] = (max ?? -1) + 1;
        }

        foreach (var card in Cards)
        {
            var targetCol = TargetColumn(card, firstCol, midCol, lastCol);
            var entity = new Card
            {
                Title = card.Title,
                Description = card.Description,
                ColumnId = targetCol.Id,
                Position = positions[targetCol.Id]++,
            };
            BuildHistory(entity, card, firstCol, midCol, lastCol, sprintStart);
            db.Cards.Add(entity);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} test cards for board {BoardId}", Cards.Length, boardId);
        return ServiceResult.Ok();
    }

    // Cards with DoneDay go to the last column; cards with DoingDay go to the mid column
    // (if it exists); everything else stays in the first column.
    private static Column TargetColumn(SprintCard card, Column firstCol, Column? midCol, Column lastCol) =>
        card.DoneDay.HasValue ? lastCol :
        card.DoingDay.HasValue && midCol is not null ? midCol :
        firstCol;

    private static void BuildHistory(
        Card entity, SprintCard card,
        Column firstCol, Column? midCol, Column lastCol,
        DateTime sprintStart)
    {
        var toDoEntered = sprintStart.AddDays(card.ToDoDay);

        // Cards still in the first column (not yet started, or in-progress on a 2-column board)
        if (!card.DoingDay.HasValue || (midCol is null && !card.DoneDay.HasValue))
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = firstCol.Id, ColumnName = firstCol.Name,
                EnteredAt = toDoEntered,
            });
            return;
        }

        var doingEntered = sprintStart.AddDays(card.DoingDay!.Value);

        // firstCol → exited when work started
        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = firstCol.Id, ColumnName = firstCol.Name,
            EnteredAt = toDoEntered, ExitedAt = doingEntered,
        });

        if (!card.DoneDay.HasValue)
        {
            // In progress — open entry in mid column
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = midCol!.Id, ColumnName = midCol!.Name,
                EnteredAt = doingEntered,
            });
            return;
        }

        var doneEntered = sprintStart.AddDays(card.DoneDay!.Value);

        // midCol exists: record the doing phase before marking done
        if (midCol is not null)
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = midCol.Id, ColumnName = midCol.Name,
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
