using System.Security.Claims;
using KanbanApi.Models;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanApi.Controllers;

[ApiController]
[Route("boards/{boardId}/columns/{columnId}/cards")]
[Authorize]
public class CardsController(ICardService cardService) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetCards(int boardId, int columnId)
    {
        var result = await cardService.GetCardsAsync(boardId, columnId, UserId);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCard(int boardId, int columnId, [FromBody] CreateCardRequest request)
    {
        var result = await cardService.CreateCardAsync(boardId, columnId, request, UserId);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return CreatedAtAction(nameof(GetCards), new { boardId, columnId }, result.Value);
    }

    [HttpPut("{cardId}")]
    public async Task<IActionResult> UpdateCard(int boardId, int columnId, int cardId, [FromBody] UpdateCardRequest request)
    {
        var result = await cardService.UpdateCardAsync(boardId, columnId, cardId, request, UserId);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }

    [HttpDelete("{cardId}")]
    public async Task<IActionResult> DeleteCard(int boardId, int columnId, int cardId)
    {
        var result = await cardService.DeleteCardAsync(boardId, columnId, cardId, UserId);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return NoContent();
    }

    [HttpPut("{cardId}/move")]
    public async Task<IActionResult> MoveCard(int boardId, int columnId, int cardId, [FromBody] MoveCardRequest request)
    {
        var result = await cardService.MoveCardAsync(boardId, columnId, cardId, request, UserId);
        if (result.IsNotFound) return NotFound();
        if (result.IsForbidden) return Forbid();
        return Ok(result.Value);
    }
}
