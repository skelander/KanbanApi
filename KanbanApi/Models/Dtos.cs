namespace KanbanApi.Models;

// Auth
public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token);
public record CreateUserRequest(string Username, string Password, string Role);
public record UserResponse(int Id, string Username, string Role);

// Boards
public record CreateBoardRequest(string Name, string? Description);
public record UpdateBoardRequest(string? Name, string? Description);
public record AddMemberRequest(int UserId);
public record BoardSummaryResponse(int Id, string Name, string? Description, int OwnerId, string OwnerUsername);
public record BoardResponse(int Id, string Name, string? Description, int OwnerId, string OwnerUsername, IEnumerable<UserResponse> Members, IEnumerable<ColumnResponse> Columns);

// Columns
public record CreateColumnRequest(string Name, int? WipLimit = null);
public record UpdateColumnRequest(string? Name, int? Position, int? WipLimit);
public record ColumnResponse(int Id, string Name, int Position, int? WipLimit, int BoardId);

// Cards
public record CreateCardRequest(string Title, string? Description);
public record UpdateCardRequest(string? Title, string? Description, int? Position);
public record MoveCardRequest(int TargetColumnId, int Position);
public record CardStateHistoryResponse(
    int ColumnId,
    string ColumnName,
    DateTime EnteredAt,
    DateOnly EnteredDate,
    DateTime? ExitedAt,
    DateOnly? ExitedDate);
public record CardResponse(int Id, string Title, string? Description, int Position, int ColumnId, IEnumerable<CardStateHistoryResponse> StateHistory);

// Service result
public enum ServiceStatus { Ok, NotFound, Forbidden }
public record ServiceResult<T>(T? Value, ServiceStatus Status)
{
    public bool IsSuccess => Status == ServiceStatus.Ok;
    public bool IsNotFound => Status == ServiceStatus.NotFound;
    public bool IsForbidden => Status == ServiceStatus.Forbidden;

    public static ServiceResult<T> Ok(T value) => new(value, ServiceStatus.Ok);
    public static ServiceResult<T> NotFound() => new(default, ServiceStatus.NotFound);
    public static ServiceResult<T> Forbidden() => new(default, ServiceStatus.Forbidden);
}
