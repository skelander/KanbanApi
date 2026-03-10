using KanbanApi.Models;

namespace KanbanApi.Services;

public interface IAuthService
{
    Task<string?> LoginAsync(string username, string password, CancellationToken ct = default);
    Task<ServiceResult<UserResponse>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<IEnumerable<UserResponse>> GetUsersAsync(CancellationToken ct = default);
    Task<ServiceResult> DeleteUserAsync(int id, CancellationToken ct = default);
}
