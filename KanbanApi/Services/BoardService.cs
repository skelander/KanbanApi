using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanbanApi.Services;

public class BoardService(AppDbContext db, ILogger<BoardService> logger) : IBoardService
{
    public async Task<IEnumerable<BoardSummaryResponse>> GetBoardsForUserAsync(int userId)
    {
        return await db.Boards
            .Where(b => b.Members.Any(m => m.UserId == userId))
            .Select(b => new BoardSummaryResponse(b.Id, b.Name, b.Description, b.OwnerId, b.Owner.Username))
            .ToListAsync();
    }

    public async Task<ServiceResult<BoardResponse>> GetBoardAsync(int boardId, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Owner)
            .Include(b => b.Members).ThenInclude(m => m.User)
            .Include(b => b.Columns.OrderBy(c => c.Position))
                .ThenInclude(c => c.Cards.OrderBy(card => card.Position))
                    .ThenInclude(card => card.StateHistory)
                        .ThenInclude(h => h.Column)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<BoardResponse>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<BoardResponse>.Forbidden();

        return ServiceResult<BoardResponse>.Ok(MapToResponse(board));
    }

    public async Task<BoardResponse> CreateBoardAsync(CreateBoardRequest request, int userId, string ownerUsername, string ownerRole)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var board = new Board
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId
        };

        db.Boards.Add(board);
        await db.SaveChangesAsync();

        db.BoardMembers.Add(new BoardMember { BoardId = board.Id, UserId = userId });

        string[] defaultColumns = ["To Do", "Doing", "Done"];
        var columns = new List<Column>();
        for (int i = 0; i < defaultColumns.Length; i++)
        {
            var col = new Column { Name = defaultColumns[i], Position = i, BoardId = board.Id };
            db.Columns.Add(col);
            columns.Add(col);
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        logger.LogInformation("Created board {BoardName} for user {UserId}", board.Name, userId);

        return new BoardResponse(
            board.Id,
            board.Name,
            board.Description,
            board.OwnerId,
            ownerUsername,
            [new UserResponse(userId, ownerUsername, ownerRole)],
            columns.Select(c => new ColumnResponse(c.Id, c.Name, c.Position, c.WipLimit, c.BoardId, []))
        );
    }

    public async Task<ServiceResult<BoardResponse>> UpdateBoardAsync(int boardId, UpdateBoardRequest request, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Owner)
            .Include(b => b.Members).ThenInclude(m => m.User)
            .Include(b => b.Columns.OrderBy(c => c.Position))
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<BoardResponse>.NotFound();
        if (!isAdmin && board.OwnerId != userId) return ServiceResult<BoardResponse>.Forbidden();

        if (request.Name is not null) board.Name = request.Name;
        if (request.Description is not null) board.Description = request.Description;

        await db.SaveChangesAsync();
        logger.LogInformation("Updated board {BoardId}", boardId);
        return ServiceResult<BoardResponse>.Ok(MapToResponse(board));
    }

    public async Task<ServiceResult<bool>> DeleteBoardAsync(int boardId, int userId, bool isAdmin = false)
    {
        var board = await db.Boards.FindAsync(boardId);
        if (board is null) return ServiceResult<bool>.NotFound();
        if (!isAdmin && board.OwnerId != userId) return ServiceResult<bool>.Forbidden();

        db.Boards.Remove(board);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted board {BoardId}", boardId);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<IEnumerable<UserResponse>>> GetMembersAsync(int boardId, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<IEnumerable<UserResponse>>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<IEnumerable<UserResponse>>.Forbidden();

        var members = board.Members.Select(m => new UserResponse(m.User.Id, m.User.Username, m.User.Role));
        return ServiceResult<IEnumerable<UserResponse>>.Ok(members);
    }

    public async Task<ServiceResult<bool>> AddMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<bool>.NotFound();
        if (!isAdmin && board.OwnerId != requestingUserId) return ServiceResult<bool>.Forbidden();

        if (board.Members.Any(m => m.UserId == targetUserId))
            return ServiceResult<bool>.Ok(true);

        var targetUser = await db.Users.FindAsync(targetUserId);
        if (targetUser is null) return ServiceResult<bool>.NotFound();

        db.BoardMembers.Add(new BoardMember { BoardId = boardId, UserId = targetUserId });
        await db.SaveChangesAsync();
        logger.LogInformation("Added user {UserId} to board {BoardId}", targetUserId, boardId);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> RemoveMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<bool>.NotFound();
        if (!isAdmin && board.OwnerId != requestingUserId) return ServiceResult<bool>.Forbidden();
        if (board.OwnerId == targetUserId) return ServiceResult<bool>.Forbidden();

        var member = board.Members.FirstOrDefault(m => m.UserId == targetUserId);
        if (member is null) return ServiceResult<bool>.NotFound();

        db.BoardMembers.Remove(member);
        await db.SaveChangesAsync();
        logger.LogInformation("Removed user {UserId} from board {BoardId}", targetUserId, boardId);
        return ServiceResult<bool>.Ok(true);
    }

    private static BoardResponse MapToResponse(Board board) => new(
        board.Id,
        board.Name,
        board.Description,
        board.OwnerId,
        board.Owner.Username,
        board.Members.Select(m => new UserResponse(m.User.Id, m.User.Username, m.User.Role)),
        board.Columns.Select(c => new ColumnResponse(c.Id, c.Name, c.Position, c.WipLimit, c.BoardId,
            c.Cards.Select(card => new CardResponse(card.Id, card.Title, card.Description, card.Position, card.ColumnId,
                card.StateHistory
                    .OrderBy(h => h.EnteredAt)
                    .Select(h => new CardStateHistoryResponse(
                        h.ColumnId,
                        h.Column.Name,
                        h.EnteredAt,
                        DateOnly.FromDateTime(h.EnteredAt),
                        h.ExitedAt,
                        h.ExitedAt.HasValue ? DateOnly.FromDateTime(h.ExitedAt.Value) : null))))))
    );
}
