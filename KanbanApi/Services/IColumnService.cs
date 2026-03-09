using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IColumnService
{
    Task<ServiceResult<IEnumerable<ColumnResponse>>> GetColumnsAsync(int boardId, int userId, bool isAdmin = false);
    Task<ServiceResult<ColumnResponse>> CreateColumnAsync(int boardId, CreateColumnRequest request, int userId, bool isAdmin = false);
    Task<ServiceResult<ColumnResponse>> UpdateColumnAsync(int boardId, int columnId, UpdateColumnRequest request, int userId, bool isAdmin = false);
    Task<ServiceResult<bool>> DeleteColumnAsync(int boardId, int columnId, int userId, bool isAdmin = false);
}
