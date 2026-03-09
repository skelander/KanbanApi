using System.Security.Claims;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanApi.Controllers;

[ApiController]
[Route("boards/{boardId}/columns")]
[Authorize]
public class ColumnsController(IColumnService columnService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private bool IsAdmin => User.IsInRole("admin");

    [HttpGet]
    public async Task<IActionResult> GetColumns(int boardId)
    {
        var result = await columnService.GetColumnsAsync(boardId, UserId, IsAdmin);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateColumn(int boardId, [FromBody] CreateColumnRequest request)
    {
        var result = await columnService.CreateColumnAsync(boardId, request, UserId, IsAdmin);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return CreatedAtAction(nameof(GetColumns), new { boardId }, result.Value);
    }

    [HttpPut("{columnId}")]
    public async Task<IActionResult> UpdateColumn(int boardId, int columnId, [FromBody] UpdateColumnRequest request)
    {
        var result = await columnService.UpdateColumnAsync(boardId, columnId, request, UserId, IsAdmin);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpDelete("{columnId}")]
    public async Task<IActionResult> DeleteColumn(int boardId, int columnId)
    {
        var result = await columnService.DeleteColumnAsync(boardId, columnId, UserId, IsAdmin);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return NoContent();
    }
}
