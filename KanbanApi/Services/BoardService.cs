using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanbanApi.Services;

public class BoardService(AppDbContext db, ILogger<BoardService> logger) : IBoardService
{
    public async Task<IEnumerable<BoardSummaryResponse>> GetBoardsForUserAsync(int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        return await db.Boards
            .Where(b => isAdmin || b.Members.Any(m => m.UserId == userId))
            .Select(b => new BoardSummaryResponse(b.Id, b.Name, b.Description, b.OwnerId, b.Owner.Username, b.Members.Count))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<BoardResponse>> GetBoardAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Owner)
            .Include(b => b.Members).ThenInclude(m => m.User)
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Cards.OrderBy(card => card.Position))
                    .ThenInclude(card => card.StateHistory)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult<BoardResponse>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<BoardResponse>.Forbidden();

        return ServiceResult<BoardResponse>.Ok(MapToResponse(board));
    }

    public async Task<ServiceResult<BoardResponse>> CreateBoardAsync(CreateBoardRequest request, int userId, string ownerUsername, string ownerRole, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var board = new Board
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId
        };

        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);

        db.BoardMembers.Add(new BoardMember { BoardId = board.Id, UserId = userId });

        var backlog = new Column { Name = "Backlog", Position = 0, IsBacklog = true, BoardId = board.Id };
        db.Columns.Add(backlog);

        string[] defaultColumns = ["Analysis started", "Analysis done", "Coding started", "Coding done", "Testing started", "Testing done", "Deployed"];
        var columns = new List<Column> { backlog };
        for (int i = 0; i < defaultColumns.Length; i++)
        {
            var col = new Column { Name = defaultColumns[i], Position = i + 1, BoardId = board.Id };
            db.Columns.Add(col);
            columns.Add(col);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Created board {BoardName} for user {UserId}", board.Name, userId);

        return ServiceResult<BoardResponse>.Ok(new BoardResponse(
            board.Id,
            board.Name,
            board.Description,
            board.OwnerId,
            ownerUsername,
            [new UserResponse(userId, ownerUsername, ownerRole)],
            columns.Select(c => new ColumnResponse(c.Id, c.Name, c.Position, c.WipLimit, c.IsBacklog, c.BoardId, []))
        ));
    }

    public async Task<ServiceResult<BoardResponse>> UpdateBoardAsync(int boardId, UpdateBoardRequest request, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Owner)
            .Include(b => b.Members).ThenInclude(m => m.User)
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Cards.OrderBy(card => card.Position))
                    .ThenInclude(card => card.StateHistory)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult<BoardResponse>.NotFound();
        if (!isAdmin && board.OwnerId != userId) return ServiceResult<BoardResponse>.Forbidden();

        if (request.Name is not null) board.Name = request.Name;
        if (request.Description is not null) board.Description = request.Description;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated board {BoardId}", boardId);
        return ServiceResult<BoardResponse>.Ok(MapToResponse(board));
    }

    public async Task<ServiceResult> DeleteBoardAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards.FindAsync([boardId], ct);
        if (board is null) return ServiceResult.NotFound();
        if (!isAdmin && board.OwnerId != userId) return ServiceResult.Forbidden();

        db.Boards.Remove(board);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deleted board {BoardId}", boardId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IEnumerable<UserResponse>>> GetMembersAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult<IEnumerable<UserResponse>>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<IEnumerable<UserResponse>>.Forbidden();

        var members = board.Members.Select(m => new UserResponse(m.User.Id, m.User.Username, m.User.Role));
        return ServiceResult<IEnumerable<UserResponse>>.Ok(members);
    }

    public async Task<ServiceResult> AddMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult.NotFound();
        if (!isAdmin && board.OwnerId != requestingUserId) return ServiceResult.Forbidden();

        if (board.Members.Any(m => m.UserId == targetUserId))
            return ServiceResult.Ok();

        var targetUser = await db.Users.FindAsync([targetUserId], ct);
        if (targetUser is null) return ServiceResult.NotFound();

        db.BoardMembers.Add(new BoardMember { BoardId = boardId, UserId = targetUserId });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Added user {UserId} to board {BoardId}", targetUserId, boardId);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult.NotFound();
        if (!isAdmin && board.OwnerId != requestingUserId) return ServiceResult.Forbidden();
        if (board.OwnerId == targetUserId) return ServiceResult.Forbidden();

        var member = board.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (member is null) return ServiceResult.NotFound();

        db.BoardMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Removed user {UserId} from board {BoardId}", targetUserId, boardId);
        return ServiceResult.Ok();
    }

    private static BoardResponse MapToResponse(Board board) => new(
        board.Id,
        board.Name,
        board.Description,
        board.OwnerId,
        board.Owner.Username,
        board.Members.Select(m => new UserResponse(m.User.Id, m.User.Username, m.User.Role)),
        board.Columns
            .OrderBy(c => c.IsBacklog ? 0 : 1).ThenBy(c => c.Position)
            .Select(c => new ColumnResponse(c.Id, c.Name, c.Position, c.WipLimit, c.IsBacklog, c.BoardId,
            c.Cards.Select(card => new CardResponse(card.Id, card.Title, card.Description, card.Position, card.ColumnId,
                card.StateHistory
                    .OrderBy(h => h.EnteredAt)
                    .Select(h => new CardStateHistoryResponse(
                        h.ColumnId,
                        h.ColumnName,
                        h.EnteredAt,
                        DateOnly.FromDateTime(h.EnteredAt),
                        h.ExitedAt,
                        h.ExitedAt.HasValue ? DateOnly.FromDateTime(h.ExitedAt.Value) : null))))))
    );
}
