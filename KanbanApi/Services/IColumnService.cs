using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IColumnService
{
    Task<ServiceResult<IEnumerable<ColumnResponse>>> GetColumnsAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default);
}
