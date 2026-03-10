using System.Security.Claims;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanApi.Controllers;

[ApiController]
[Route("boards")]
[Authorize]
public class BoardsController(IBoardService boardService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("admin");

    [HttpGet]
    public async Task<IActionResult> GetBoards(CancellationToken ct)
    {
        var boards = await boardService.GetBoardsForUserAsync(UserId, IsAdmin, ct);
        return Ok(boards);
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> CreateBoard([FromBody] CreateBoardRequest request, CancellationToken ct)
    {
        var username = User.FindFirstValue(ClaimTypes.Name)!;
        var role = User.FindFirstValue(ClaimTypes.Role)!;
        var result = await boardService.CreateBoardAsync(request, UserId, username, role, ct);
        return CreatedAtAction(nameof(GetBoard), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBoard(int id, CancellationToken ct)
    {
        var result = await boardService.GetBoardAsync(id, UserId, IsAdmin, ct);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBoard(int id, [FromBody] UpdateBoardRequest request, CancellationToken ct)
    {
        var result = await boardService.UpdateBoardAsync(id, request, UserId, IsAdmin, ct);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBoard(int id, CancellationToken ct)
    {
        var result = await boardService.DeleteBoardAsync(id, UserId, IsAdmin, ct);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return NoContent();
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(int id, CancellationToken ct)
    {
        var result = await boardService.GetMembersAsync(id, UserId, IsAdmin, ct);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        var result = await boardService.AddMemberAsync(id, request.UserId, UserId, IsAdmin, ct);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, int userId, CancellationToken ct)
    {
        var result = await boardService.RemoveMemberAsync(id, userId, UserId, IsAdmin, ct);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return NoContent();
    }
}
