using System.ComponentModel.DataAnnotations;

namespace KanbanApi.Models;

// Auth
public record LoginRequest(
    [Required] string Username,
    [Required] string Password);

public record LoginResponse(string Token);

public record CreateUserRequest(
    [Required][MaxLength(100)] string Username,
    [Required][MinLength(4)] string Password,
    [Required] string Role)
{
    public static readonly string[] AllowedRoles = ["user", "admin"];
}

public record UserResponse(int Id, string Username, string Role);

// Boards
public record CreateBoardRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description);

public record UpdateBoardRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(1000)] string? Description);

public record AddMemberRequest([Required] int UserId);

public record BoardSummaryResponse(int Id, string Name, string? Description, int OwnerId, string OwnerUsername);
public record BoardResponse(int Id, string Name, string? Description, int OwnerId, string OwnerUsername, IEnumerable<UserResponse> Members, IEnumerable<ColumnResponse> Columns);

// Columns
public record CreateColumnRequest(
    [Required][MaxLength(100)] string Name,
    [Range(1, 1000)] int? WipLimit = null);

public record UpdateColumnRequest(
    [MaxLength(100)] string? Name,
    [Range(0, int.MaxValue)] int? Position,
    [Range(1, 1000)] int? WipLimit);

public record ColumnResponse(int Id, string Name, int Position, int? WipLimit, int BoardId);

// Cards
public record CreateCardRequest(
    [Required][MaxLength(500)] string Title,
    [MaxLength(5000)] string? Description);

public record UpdateCardRequest(
    [MaxLength(500)] string? Title,
    [MaxLength(5000)] string? Description,
    [Range(0, int.MaxValue)] int? Position);

public record MoveCardRequest(
    [Required] int TargetColumnId,
    [Range(0, int.MaxValue)] int Position);

public record CardStateHistoryResponse(
    int ColumnId,
    string ColumnName,
    DateTime EnteredAt,
    DateOnly EnteredDate,
    DateTime? ExitedAt,
    DateOnly? ExitedDate);

public record CardResponse(int Id, string Title, string? Description, int Position, int ColumnId, IEnumerable<CardStateHistoryResponse> StateHistory);

// Service result
public enum ServiceStatus { Ok, NotFound, Forbidden, Conflict }
public record ServiceResult<T>(T? Value, ServiceStatus Status)
{
    public bool IsSuccess => Status == ServiceStatus.Ok;
    public bool IsNotFound => Status == ServiceStatus.NotFound;
    public bool IsForbidden => Status == ServiceStatus.Forbidden;
    public bool IsConflict => Status == ServiceStatus.Conflict;

    public static ServiceResult<T> Ok(T value) => new(value, ServiceStatus.Ok);
    public static ServiceResult<T> NotFound() => new(default, ServiceStatus.NotFound);
    public static ServiceResult<T> Forbidden() => new(default, ServiceStatus.Forbidden);
    public static ServiceResult<T> Conflict() => new(default, ServiceStatus.Conflict);
}
