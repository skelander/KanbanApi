using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
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
        if (result.IsConflict) return Conflict(new { error = "Username already exists." });
        return Created($"/auth/users/{result.Value!.Id}", result.Value);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var deleted = await authService.DeleteUserAsync(id);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
