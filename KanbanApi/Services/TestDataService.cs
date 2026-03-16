using System.Text.Json;
using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public interface ITestDataService
{
    Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default);
    Task<ServiceResult> SeedBacklogAsync(int boardId, CancellationToken ct = default);
    Task<ServiceResult> SeedMidSprintAsync(int boardId, CancellationToken ct = default);
    Task<string?> GetDatasetAsync(string name, CancellationToken ct = default);
    Task<bool> UpdateDatasetAsync(string name, string dataJson, CancellationToken ct = default);
}

public class TestDataService(AppDbContext db, ILogger<TestDataService> logger) : ITestDataService
{
    public record SprintCard(string Title, string? Description, int ToDoDay, int? DoingDay, int? DoneDay);
    // BacklogAgo: when the card was added to the backlog (days before today)
    // TodoAgo: when it was pulled from the backlog (null = still in backlog)
    public record MultiSprintCard(string Title, string? Description, int BacklogAgo, int? TodoAgo, int? DoingAgo, int? DoneAgo, int DoingColOffset = 0);

    private static readonly string[] DefaultBacklogItems =
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
    private static readonly SprintCard[] DefaultMidSprintCards =
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
    // All cards enter the Backlog before sprint 1 (75–70 days ago).
    // Each sprint, cards are pulled from the Backlog. Unpulled cards remain there.
    // BacklogAgo: days before today when card was added to backlog.
    // TodoAgo: days before today when pulled from backlog (null = still in backlog).
    // DoingColOffset: -2=Analysis done, -1=Coding started, 0=Coding done (default), +1=Testing started, +2=Testing done
    private static readonly MultiSprintCard[] DefaultMultiSprintCards =
    [
        // Sprint 1 (70–56 days ago) — all deployed
        new("Project setup and CI/CD",       "GitHub repo, branch protection, Actions pipeline, team access.",           75, 69, 68, 65,  DoingColOffset: -1),
        new("Domain model and migrations",   "Define entities, EF Core relationships, run initial migration.",           75, 69, 66, 61,  DoingColOffset:  0),
        new("JWT authentication",            "HS256 token issuance, role claims, token validation middleware.",          74, 68, 64, 59,  DoingColOffset: +1),
        new("Core API scaffolding",          "Controller routing, global error handler, health check endpoint.",         74, 67, 62, 57,  DoingColOffset:  0),

        // Sprint 2 (56–42 days ago) — all deployed
        new("Board CRUD",                    "Create, read, update, delete boards with owner-only enforcement.",         75, 55, 54, 51,  DoingColOffset:  0),
        new("Column management",             "Create, reorder, and delete columns per board.",                           75, 55, 52, 47,  DoingColOffset: +1),
        new("Card lifecycle",                "Create, edit, move, delete cards; auto-incrementing positions.",           74, 54, 50, 45,  DoingColOffset: -1),
        new("WIP limits",                    "Per-column WIP cap; 409 Conflict when at limit on create or move.",        74, 53, 48, 43,  DoingColOffset:  0),

        // Sprint 3 (42–28 days ago) — all deployed
        new("Board membership",              "Invite and remove members; owner and admin permission model.",             75, 41, 40, 37,  DoingColOffset: +1),
        new("Card state history",            "Track column transitions with entered/exited timestamps.",                 75, 41, 38, 33,  DoingColOffset:  0),
        new("Drag-and-drop reordering",      "Reorder cards within and across columns via position updates.",            74, 40, 36, 31,  DoingColOffset: -1),
        new("Integration test suite",        "WebApplicationFactory tests covering happy path and error cases.",         74, 39, 34, 29,  DoingColOffset:  0),

        // Sprint 4 (28–14 days ago) — all deployed
        new("Work item age chart",           "SVG scatter: X = column, Y = days since leaving Backlog.",                74, 27, 26, 23,  DoingColOffset: +1),
        new("Sprint navigation",             "Step through historical board snapshots by sprint.",                       74, 27, 24, 19,  DoingColOffset:  0),
        new("SLE reference lines",           "50th and 85th percentile lines from prior sprint cycle times.",           73, 26, 22, 17,  DoingColOffset: -1),
        new("Test data seeding",             "Multi-sprint seed; cards pulled from Backlog per sprint.",                 73, 25, 20, 15,  DoingColOffset:  0),

        // Sprint 5 (14–0 days ago, current) — 1 deployed, 4 in flight, 2 in Analysis started
        new("Board analytics dashboard",     "Throughput, cycle time distribution, and CFD overview.",                  72, 13, 12,  9,  DoingColOffset: +1),  // Deployed
        new("Card search and filter",        "Filter cards by title and label across all columns.",                      72, 12,  8,  null, DoingColOffset: -1), // Coding started
        new("Notification preferences",      "Per-user settings for in-app and email notifications.",                   71, 11,  7,  null, DoingColOffset:  0), // Coding done
        new("Assignee field on cards",       "Assign a board member to a card; filter by assignee.",                    71, 10,  5,  null, DoingColOffset: +1), // Testing started
        new("Sprint planning view",          "Pull cards from Backlog into a sprint with capacity tracking.",            70,  9,  null, null),                   // Analysis started
        new("Backlog prioritisation",        "Drag to reorder Backlog items; priority score field on cards.",           70,  8,  null, null),                   // Analysis started

        // Still in Backlog — never pulled
        new("Export board as PDF",           null,                                                                        75, null, null, null),
        new("Email notifications",           "Notify members when cards are assigned or moved.",                         75, null, null, null),
        new("Recurring card templates",      null,                                                                        74, null, null, null),
        new("Attach files to cards",         null,                                                                        72, null, null, null),
        new("Card comments and @mentions",   null,                                                                        70, null, null, null),
        new("Time tracking per card",        "Log time spent; show totals per card and per sprint.",                     68, null, null, null),
        new("Custom labels and tags",        "Color-coded labels for categorising cards.",                               67, null, null, null),
        new("Board templates library",       "Save a board as a template and create new boards from it.",                66, null, null, null),
        new("Two-factor authentication",     null,                                                                        65, null, null, null),
        new("Archive completed cards",       "Move done cards to an archive instead of deleting them.",                  63, null, null, null),
        new("Card dependencies",             "Mark one card as blocked by another.",                                     62, null, null, null),
        new("Guest access / public boards",  "Share a read-only board link without requiring login.",                    60, null, null, null),
        new("CSV export",                    "Export card data and cycle times as CSV for further analysis.",             58, null, null, null),
        new("Slack integration",             "Post card move notifications to a Slack channel.",                         55, null, null, null),
        new("Swimlanes",                     "Group cards horizontally by assignee, epic, or priority.",                 50, null, null, null),
        new("Card age alerts",               "Highlight cards that have exceeded their SLE threshold.",                  45, null, null, null),
        new("Calendar view",                 "Show cards with due dates on a monthly calendar.",                         40, null, null, null),
        new("Native mobile app",             null,                                                                        35, null, null, null),
        new("Offline support",               "Cache board state locally; sync when back online.",                        30, null, null, null),
    ];

    private static readonly string[] MultiSprintColumns = ["Analysis started", "Analysis done", "Coding started", "Coding done", "Testing started", "Testing done", "Deployed"];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public const string MultiSprintDatasetName = "multisprint_v2"; // v2: added BacklogAgo field
    public const string MidSprintDatasetName = "midsprint";
    public const string BacklogDatasetName = "backlog";

    public static readonly IReadOnlySet<string> ValidDatasetNames =
        new HashSet<string> { MultiSprintDatasetName, MidSprintDatasetName, BacklogDatasetName, "multisprint" }; // keep old name readable via GET

    public async Task<string?> GetDatasetAsync(string name, CancellationToken ct = default)
    {
        if (!ValidDatasetNames.Contains(name)) return null;
        var stored = await db.SeedDatasets.FindAsync(new object[] { name }, ct);
        if (stored is not null) return stored.DataJson;

        // Return defaults (without persisting — no board context here)
        return name switch
        {
            MultiSprintDatasetName => JsonSerializer.Serialize(DefaultMultiSprintCards),
            MidSprintDatasetName => JsonSerializer.Serialize(DefaultMidSprintCards),
            BacklogDatasetName => JsonSerializer.Serialize(DefaultBacklogItems),
            _ => null
        };
    }

    public async Task<bool> UpdateDatasetAsync(string name, string dataJson, CancellationToken ct = default)
    {
        if (!ValidDatasetNames.Contains(name)) return false;
        var stored = await db.SeedDatasets.FindAsync(new object[] { name }, ct);
        if (stored is null)
            db.SeedDatasets.Add(new SeedDataset { Name = name, DataJson = dataJson });
        else
            stored.DataJson = dataJson;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<ServiceResult> SeedAsync(int boardId, CancellationToken ct = default) =>
        SeedMultiSprintAsync(boardId, ct);

    public Task<ServiceResult> SeedMidSprintAsync(int boardId, CancellationToken ct = default) =>
        SeedSprintAsync(boardId, MidSprintDatasetName, DefaultMidSprintCards, daysAgo: 7, ct);

    private async Task<SprintCard[]> LoadSprintCardsAsync(string name, SprintCard[] defaults, CancellationToken ct)
    {
        var stored = await db.SeedDatasets.FindAsync(new object[] { name }, ct);
        if (stored is not null)
            return JsonSerializer.Deserialize<SprintCard[]>(stored.DataJson, JsonOptions) ?? defaults;
        db.SeedDatasets.Add(new SeedDataset { Name = name, DataJson = JsonSerializer.Serialize(defaults) });
        await db.SaveChangesAsync(ct);
        return defaults;
    }

    private async Task<ServiceResult> SeedSprintAsync(int boardId, string datasetName, SprintCard[] defaults, int daysAgo, CancellationToken ct)
    {
        var columns = await db.Columns
            .Where(c => c.BoardId == boardId)
            .OrderBy(c => c.Position)
            .ToListAsync(ct);

        if (columns.Count == 0) return ServiceResult.NotFound();

        var cards = await LoadSprintCardsAsync(datasetName, defaults, ct);

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

        var stored = await db.SeedDatasets.FindAsync(new object[] { MultiSprintDatasetName }, ct);
        MultiSprintCard[] cards;
        if (stored is not null)
        {
            cards = JsonSerializer.Deserialize<MultiSprintCard[]>(stored.DataJson, JsonOptions) ?? DefaultMultiSprintCards;
        }
        else
        {
            cards = DefaultMultiSprintCards;
            db.SeedDatasets.Add(new SeedDataset { Name = MultiSprintDatasetName, DataJson = JsonSerializer.Serialize(DefaultMultiSprintCards) });
        }

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

        foreach (var card in cards)
        {
            Column? cardDoingCol = null;
            if (card.DoingAgo.HasValue)
            {
                var colIdx = Math.Clamp(midIdx + card.DoingColOffset, 1, workCols.Count - 2);
                cardDoingCol = workCols[colIdx];
            }

            var targetCol = !card.TodoAgo.HasValue ? backlogCol
                : card.DoneAgo.HasValue ? lastCol
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
        logger.LogInformation("Seeded {Count} multi-sprint cards for board {BoardId}", cards.Length, boardId);
        return ServiceResult.Ok();
    }

    private static void BuildAbsoluteHistory(
        Card entity, MultiSprintCard card,
        Column backlogCol, Column toDoCol, Column? doingCol, Column lastCol,
        DateTime now)
    {
        var backlogEntered = now.AddDays(-card.BacklogAgo);

        if (!card.TodoAgo.HasValue)
        {
            // Card never pulled — still in backlog
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = backlogCol.Id, ColumnName = backlogCol.Name,
                EnteredAt = backlogEntered,
            });
            return;
        }

        var toDoEntered = now.AddDays(-card.TodoAgo.Value);

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

        var stored = await db.SeedDatasets.FindAsync(new object[] { BacklogDatasetName }, ct);
        string[] items;
        if (stored is not null)
        {
            items = JsonSerializer.Deserialize<string[]>(stored.DataJson, JsonOptions) ?? DefaultBacklogItems;
        }
        else
        {
            items = DefaultBacklogItems;
            db.SeedDatasets.Add(new SeedDataset { Name = BacklogDatasetName, DataJson = JsonSerializer.Serialize(DefaultBacklogItems) });
        }

        var columnIds = columns.Select(c => c.Id).ToList();
        var existing = await db.Cards.Where(c => columnIds.Contains(c.ColumnId)).ToListAsync(ct);
        db.Cards.RemoveRange(existing);

        var now = DateTime.UtcNow;
        for (int i = 0; i < items.Length; i++)
        {
            var entity = new Card
            {
                Title = items[i],
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
        logger.LogInformation("Seeded {Count} backlog cards for board {BoardId}", items.Length, boardId);
        return ServiceResult.Ok();
    }

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

        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = backlogCol.Id, ColumnName = backlogCol.Name,
            EnteredAt = backlogEntered, ExitedAt = toDoEntered,
        });

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

        entity.StateHistory.Add(new CardStateHistory
        {
            ColumnId = toDoCol.Id, ColumnName = toDoCol.Name,
            EnteredAt = toDoEntered, ExitedAt = doingEntered,
        });

        if (!card.DoneDay.HasValue)
        {
            entity.StateHistory.Add(new CardStateHistory
            {
                ColumnId = doingCol!.Id, ColumnName = doingCol!.Name,
                EnteredAt = doingEntered,
            });
            return;
        }

        var doneEntered = sprintStart.AddDays(card.DoneDay!.Value);

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
}
