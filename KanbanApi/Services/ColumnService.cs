using KanbanApi.Data;
using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Services;

public class ColumnService(AppDbContext db) : IColumnService
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

}
