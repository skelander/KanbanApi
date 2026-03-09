using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IColumnService
{
    Task<ServiceResult<IEnumerable<ColumnResponse>>> GetColumnsAsync(int boardId, int userId);
    Task<ServiceResult<ColumnResponse>> CreateColumnAsync(int boardId, CreateColumnRequest request, int userId);
    Task<ServiceResult<ColumnResponse>> UpdateColumnAsync(int boardId, int columnId, UpdateColumnRequest request, int userId);
    Task<ServiceResult<bool>> DeleteColumnAsync(int boardId, int columnId, int userId);
}
