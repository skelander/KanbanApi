using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IBoardService
{
    Task<IEnumerable<BoardSummaryResponse>> GetBoardsForUserAsync(int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<BoardResponse>> GetBoardAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<BoardResponse>> CreateBoardAsync(CreateBoardRequest request, int userId, string ownerUsername, string ownerRole, CancellationToken ct = default);
    Task<ServiceResult<BoardResponse>> UpdateBoardAsync(int boardId, UpdateBoardRequest request, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult> DeleteBoardAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult<IEnumerable<UserResponse>>> GetMembersAsync(int boardId, int userId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult> AddMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false, CancellationToken ct = default);
    Task<ServiceResult> RemoveMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false, CancellationToken ct = default);
}
