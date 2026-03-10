using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace KanbanApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await authService.LoginAsync(request.Username, request.Password);
        if (token is null) return Unauthorized();
        return Ok(new LoginResponse(token));
    }

    [Authorize(Roles = "admin")]
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await authService.GetUsersAsync();
        return Ok(users);
    }

    [Authorize(Roles = "admin")]
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await authService.CreateUserAsync(request);
        if (result.IsForbidden) return BadRequest(new { error = $"Invalid role. Allowed values: {string.Join(", ", CreateUserRequest.AllowedRoles)}." });
        if (result.IsConflict) return Conflict(new { error = "Username already exists." });
        return Created($"/auth/users/{result.Value!.Id}", result.Value);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var result = await authService.DeleteUserAsync(id);
        if (result.IsNotFound) return NotFound();
        if (result.IsConflict) return Conflict(new { error = "Cannot delete the last admin account." });
        return NoContent();
    }
}
