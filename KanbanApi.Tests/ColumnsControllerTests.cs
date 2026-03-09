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
    public async Task CreateColumn_AsMember_ReturnsCreated()
    {
        var board = await CreateBoardAsync();
        // Board starts with 3 default columns (positions 0-2), new column gets position 3
        var response = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Review"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var column = await response.Content.ReadFromJsonAsync<ColumnResponse>();
        Assert.Equal("Review", column!.Name);
        Assert.Equal(3, column.Position);
    }

    [Fact]
    public async Task CreateMultipleColumns_PositionsIncrement()
    {
        var board = await CreateBoardAsync();
        // Board starts with 3 default columns (positions 0-2)
        var r1 = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Col 4"));
        var r2 = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Col 5"));
        var r3 = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Col 6"));
        var c1 = (await r1.Content.ReadFromJsonAsync<ColumnResponse>())!;
        var c2 = (await r2.Content.ReadFromJsonAsync<ColumnResponse>())!;
        var c3 = (await r3.Content.ReadFromJsonAsync<ColumnResponse>())!;
        Assert.True(c1.Position < c2.Position && c2.Position < c3.Position);
    }

    [Fact]
    public async Task GetColumns_ReturnsSortedByPosition()
    {
        var board = await CreateBoardAsync();
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Extra 1"));
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Extra 2"));
        var response = await _client.GetAsync($"/boards/{board.Id}/columns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var columns = await response.Content.ReadFromJsonAsync<List<ColumnResponse>>();
        // 3 defaults + 2 added = 5
        Assert.Equal(5, columns!.Count);
        for (int i = 1; i < columns.Count; i++)
            Assert.True(columns[i - 1].Position <= columns[i].Position);
    }

    [Fact]
    public async Task UpdateColumn_ChangeName_ReturnsUpdated()
    {
        var board = await CreateBoardAsync();
        var createResponse = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Old Name"));
        var column = await createResponse.Content.ReadFromJsonAsync<ColumnResponse>();
        var response = await _client.PutAsJsonAsync($"/boards/{board.Id}/columns/{column!.Id}", new UpdateColumnRequest("New Name", null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ColumnResponse>();
        Assert.Equal("New Name", updated!.Name);
    }

    [Fact]
    public async Task DeleteColumn_AsMember_ReturnsNoContent()
    {
        var board = await CreateBoardAsync();
        var createResponse = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns", new CreateColumnRequest("Delete Me"));
        var column = await createResponse.Content.ReadFromJsonAsync<ColumnResponse>();
        var response = await _client.DeleteAsync($"/boards/{board.Id}/columns/{column!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
        // Create a board as a regular user (admin is not a member)
        var adminToken = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(adminToken);
        var ownerUsername = $"boardowner_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(ownerUsername, "pass1234", "user"));
        var ownerToken = await Helpers.LoginAsync(_client, ownerUsername, "pass1234");
        _client.SetBearer(ownerToken);
        var boardResponse = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Private Board", null));
        var board = (await boardResponse.Content.ReadFromJsonAsync<BoardResponse>())!;

        // Admin (not a member) should bypass the membership check
        _client.SetBearer(adminToken);
        var response = await _client.GetAsync($"/boards/{board.Id}/columns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
