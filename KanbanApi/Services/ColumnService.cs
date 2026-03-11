using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KanbanApi.Services;

public class ColumnService(AppDbContext db, ILogger<ColumnService> logger) : IColumnService
{
    public async Task<ServiceResult<IEnumerable<ColumnResponse>>> GetColumnsAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult<IEnumerable<ColumnResponse>>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<IEnumerable<ColumnResponse>>.Forbidden();

        var columns = board.Columns
            .OrderBy(c => c.IsBacklog ? 0 : 1).ThenBy(c => c.Position)
            .Select(c => new ColumnResponse(c.Id, c.Name, c.Position, c.WipLimit, c.IsBacklog, c.BoardId, []));

        return ServiceResult<IEnumerable<ColumnResponse>>.Ok(columns);
    }

    public async Task<ServiceResult<ColumnResponse>> UpdateColumnAsync(int boardId, int columnId, UpdateColumnRequest request, int userId, bool isAdmin = false, CancellationToken ct = default)
    {
        var board = await db.Boards
            .Include(b => b.Members)
            .Include(b => b.Columns)
            .FirstOrDefaultAsync(b => b.Id == boardId, ct);

        if (board is null) return ServiceResult<ColumnResponse>.NotFound();
        if (!isAdmin && !board.Members.Any(m => m.UserId == userId))
            return ServiceResult<ColumnResponse>.Forbidden();

        var column = board.Columns.FirstOrDefault(c => c.Id == columnId);
        if (column is null) return ServiceResult<ColumnResponse>.NotFound();

        if (request.Name is not null) column.Name = request.Name;
        if (request.Position.HasValue && !column.IsBacklog) column.Position = request.Position.Value;
        if (request.WipLimit.HasValue) column.WipLimit = request.WipLimit.Value;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Updated column {ColumnId} on board {BoardId}", columnId, boardId);
        return ServiceResult<ColumnResponse>.Ok(new ColumnResponse(column.Id, column.Name, column.Position, column.WipLimit, column.IsBacklog, column.BoardId, []));
    }
}
