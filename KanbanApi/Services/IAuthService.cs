using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IAuthService
{
    Task<string?> LoginAsync(string username, string password);
    Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request);
    Task<IEnumerable<UserResponse>> GetUsersAsync();
    Task<ServiceResult<bool>> DeleteUserAsync(int id);
}
