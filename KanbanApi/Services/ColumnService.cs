using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanbanApi.Services;

public class ColumnService(AppDbContext db, ILogger<ColumnService> logger) : IColumnService
{
    public async Task<ServiceResult<IEnumerable<ColumnResponse>>> GetColumnsAsync(int boardId, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<IEnumerable<ColumnResponse>>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<IEnumerable<ColumnResponse>>.Forbidden();

        var columns = board.Columns
            .OrderBy(c => c.Position)
            .Select(c => new ColumnResponse(c.Id, c.Name, c.Position, c.WipLimit, c.BoardId, []));

        return ServiceResult<IEnumerable<ColumnResponse>>.Ok(columns);
    }

    public async Task<ServiceResult<ColumnResponse>> CreateColumnAsync(int boardId, CreateColumnRequest request, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<ColumnResponse>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<ColumnResponse>.Forbidden();

        var position = board.Columns.Count > 0 ? board.Columns.Max(c => c.Position) + 1 : 0;
        var column = new Column { Name = request.Name, Position = position, WipLimit = request.WipLimit, BoardId = boardId };

        db.Columns.Add(column);
        await db.SaveChangesAsync();

        logger.LogInformation("Created column {ColumnName} on board {BoardId}", column.Name, boardId);
        return ServiceResult<ColumnResponse>.Ok(new ColumnResponse(column.Id, column.Name, column.Position, column.WipLimit, column.BoardId, []));
    }

    public async Task<ServiceResult<ColumnResponse>> UpdateColumnAsync(int boardId, int columnId, UpdateColumnRequest request, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<ColumnResponse>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<ColumnResponse>.Forbidden();

        var column = board.Columns.FirstOrDefault(c => c.Id == columnId);
        if (column is null) return ServiceResult<ColumnResponse>.NotFound();

        if (request.Name is not null) column.Name = request.Name;
        if (request.Position.HasValue) column.Position = request.Position.Value;
        if (request.WipLimit.HasValue) column.WipLimit = request.WipLimit.Value;

        await db.SaveChangesAsync();
        return ServiceResult<ColumnResponse>.Ok(new ColumnResponse(column.Id, column.Name, column.Position, column.WipLimit, column.BoardId, []));
    }

    public async Task<ServiceResult<bool>> DeleteColumnAsync(int boardId, int columnId, int userId, bool isAdmin = false)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .Include(b => b.Columns)
                .ThenInclude(c => c.Cards)
                    .ThenInclude(card => card.StateHistory)
            .FirstOrDefaultAsync(b => b.Id == boardId);

        if (board is null) return ServiceResult<bool>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<bool>.Forbidden();

        var column = board.Columns.FirstOrDefault(c => c.Id == columnId);
        if (column is null) return ServiceResult<bool>.NotFound();

        foreach (var card in column.Cards)
            db.CardStateHistories.RemoveRange(card.StateHistory);
        db.Cards.RemoveRange(column.Cards);
        db.Columns.Remove(column);
        await db.SaveChangesAsync();
        logger.LogInformation("Deleted column {ColumnId} from board {BoardId}", columnId, boardId);
        return ServiceResult<bool>.Ok(true);
    }
}
