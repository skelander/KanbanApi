using System.Text.Json;
using KanbanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KanbanApi.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
public class TestDataController(ITestDataService testDataService) : ControllerBase
{
    [HttpPost("boards/{boardId}/testdata")]
    public async Task<IActionResult> Seed(int boardId, CancellationToken ct)
    {
        var result = await testDataService.SeedAsync(boardId, ct);
        if (result.IsNotFound) return NotFound();
        return NoContent();
    }

    [HttpPost("boards/{boardId}/testdata/backlog")]
    public async Task<IActionResult> SeedBacklog(int boardId, CancellationToken ct)
    {
        var result = await testDataService.SeedBacklogAsync(boardId, ct);
        if (result.IsNotFound) return NotFound();
        return NoContent();
    }

    [HttpPost("boards/{boardId}/testdata/midsprint")]
    public async Task<IActionResult> SeedMidSprint(int boardId, CancellationToken ct)
    {
        var result = await testDataService.SeedMidSprintAsync(boardId, ct);
        if (result.IsNotFound) return NotFound();
        return NoContent();
    }

    [HttpGet("testdata/datasets/{name}")]
    public async Task<IActionResult> GetDataset(string name, CancellationToken ct)
    {
        var json = await testDataService.GetDatasetAsync(name, ct);
        if (json is null) return NotFound();
        return Content(json, "application/json");
    }

    [HttpPut("testdata/datasets/{name}")]
    public async Task<IActionResult> UpdateDataset(string name, [FromBody] JsonElement data, CancellationToken ct)
    {
        if (!TestDataService.ValidDatasetNames.Contains(name)) return NotFound();
        var json = JsonSerializer.Serialize(data);
        await testDataService.UpdateDatasetAsync(name, json, ct);
        return NoContent();
    }
}
