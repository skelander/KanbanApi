using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IBoardService
{
    Task<IEnumerable<BoardSummaryResponse>> GetBoardsForUserAsync(int userId);
    Task<ServiceResult<BoardResponse>> GetBoardAsync(int boardId, int userId, bool isAdmin = false);
    Task<ServiceResult<BoardResponse>> CreateBoardAsync(CreateBoardRequest request, int userId, string ownerUsername, string ownerRole);
    Task<ServiceResult<BoardResponse>> UpdateBoardAsync(int boardId, UpdateBoardRequest request, int userId, bool isAdmin = false);
    Task<ServiceResult<bool>> DeleteBoardAsync(int boardId, int userId, bool isAdmin = false);
    Task<ServiceResult<IEnumerable<UserResponse>>> GetMembersAsync(int boardId, int userId, bool isAdmin = false);
    Task<ServiceResult<bool>> AddMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false);
    Task<ServiceResult<bool>> RemoveMemberAsync(int boardId, int targetUserId, int requestingUserId, bool isAdmin = false);
}
