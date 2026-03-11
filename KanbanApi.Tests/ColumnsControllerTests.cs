using System.Net;
using System.Net.Http.Json;
using KanbanApi.Models;

namespace KanbanApi.Tests;

public class ColumnsControllerTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> AdminTokenAsync()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        return token;
    }

    private async Task<BoardResponse> CreateBoardAsync()
    {
        await AdminTokenAsync();
        var response = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Column Test Board", null));
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    [Fact]
    public async Task GetColumns_ReturnsDefaultColumns()
    {
        var board = await CreateBoardAsync();
        var response = await _client.GetAsync($"/boards/{board.Id}/columns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var columns = await response.Content.ReadFromJsonAsync<List<ColumnResponse>>();
        // 4 defaults: Backlog + To Do + Doing + Done
        Assert.Equal(4, columns!.Count);
        Assert.True(columns[0].IsBacklog);
        for (int i = 1; i < columns.Count; i++)
            Assert.True(columns[i - 1].Position <= columns[i].Position);
    }

    [Fact]
    public async Task UpdateColumn_ChangeName_ReturnsUpdated()
    {
        var board = await CreateBoardAsync();
        // Use the "To Do" column (index 1 after backlog)
        var column = board.Columns.ToList()[1];
        var response = await _client.PutAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}", new UpdateColumnRequest("New Name", null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ColumnResponse>();
        Assert.Equal("New Name", updated!.Name);
    }

    [Fact]
    public async Task UpdateColumn_SetWipLimit_ReturnsUpdatedWipLimit()
    {
        var board = await CreateBoardAsync();
        // Use the "To Do" column (index 1 after backlog)
        var column = board.Columns.ToList()[1];
        Assert.Null(column.WipLimit);

        var response = await _client.PutAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}", new UpdateColumnRequest(null, null, 5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ColumnResponse>();
        Assert.Equal(5, updated!.WipLimit);
    }

    [Fact]
    public async Task GetColumns_AsNonMember_ReturnsForbid()
    {
        var board = await CreateBoardAsync();
        var adminToken = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(adminToken);
        var outsiderUsername = $"outsider_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(outsiderUsername, "pass1234", "user"));
        var outsiderToken = await Helpers.LoginAsync(_client, outsiderUsername, "pass1234");
        _client.SetBearer(outsiderToken);
        var response = await _client.GetAsync($"/boards/{board.Id}/columns");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetColumns_BoardNotFound_ReturnsNotFound()
    {
        await AdminTokenAsync();
        var response = await _client.GetAsync("/boards/99999/columns");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetColumns_AsAdmin_CanAccessAnyBoard()
    {
        // Create a second admin who owns a board (first admin is not a member)
        var adminToken = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(adminToken);
        var admin2Username = $"admin2_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(admin2Username, "pass1234", "admin"));
        var admin2Token = await Helpers.LoginAsync(_client, admin2Username, "pass1234");
        _client.SetBearer(admin2Token);
        var boardResponse = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Private Board", null));
        var board = (await boardResponse.Content.ReadFromJsonAsync<BoardResponse>())!;

        // First admin (not a member) should bypass the membership check
        _client.SetBearer(adminToken);
        var response = await _client.GetAsync($"/boards/{board.Id}/columns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
