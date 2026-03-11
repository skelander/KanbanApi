using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanApi.Controllers;

[ApiController]
[Route("boards/{boardId}/testdata")]
[Authorize(Roles = "admin")]
public class TestDataController(ITestDataService testDataService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Seed(int boardId, CancellationToken ct)
    {
        var result = await testDataService.SeedAsync(boardId, ct);
        if (result.IsNotFound) return NotFound();
        return NoContent();
    }
}
