using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IColumnService
{
    Task<ServiceResult<IEnumerable<ColumnResponse>>> GetColumnsAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<ColumnResponse>> CreateColumnAsync(int boardId, CreateColumnRequest request, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<ColumnResponse>> UpdateColumnAsync(int boardId, int columnId, UpdateColumnRequest request, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult> DeleteColumnAsync(int boardId, int columnId, int userId, bool isAdmin = false, CancellationToken ct = default);
}
