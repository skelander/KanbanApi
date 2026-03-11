using System.Net;
using System.Net.Http.Json;
using KanbanApi.Models;

namespace KanbanApi.Tests;

public class BoardsControllerTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<string> AdminTokenAsync() => await Helpers.LoginAsync(_client, "admin", "admin");

    private async Task<UserResponse> CreateUserAsync(string username)
    {
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(username, "pass", "user"));
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    private async Task<BoardResponse> CreateBoardAsync(string name = "Test Board")
    {
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest(name, null));
        return (await response.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    [Fact]
    public async Task GetBoards_WithoutAuth_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/boards");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_AsAdmin_ReturnsCreated()
    {
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("My Board", "A description"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<BoardResponse>();
        Assert.Equal("My Board", board!.Name);
        Assert.Equal("A description", board.Description);
        Assert.Single(board.Members);
    }

    [Fact]
    public async Task CreateBoard_HasDefaultColumns()
    {
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Default Columns Board", null));
        var board = await response.Content.ReadFromJsonAsync<BoardResponse>();
        var columns = board!.Columns.ToList();
        Assert.Equal(4, columns.Count);
        Assert.Equal("Backlog", columns[0].Name);
        Assert.True(columns[0].IsBacklog);
        Assert.Equal("To Do", columns[1].Name);
        Assert.Equal("Doing", columns[2].Name);
        Assert.Equal("Done", columns[3].Name);
        Assert.Equal(0, columns[0].Position);
        Assert.Equal(1, columns[1].Position);
        Assert.Equal(2, columns[2].Position);
        Assert.Equal(3, columns[3].Position);
    }

    [Fact]
    public async Task GetBoards_ReturnsOnlyMemberBoards()
    {
        var board = await CreateBoardAsync("Admin Board");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.GetAsync("/boards");
        var boards = await response.Content.ReadFromJsonAsync<List<BoardSummaryResponse>>();
        Assert.Contains(boards!, b => b.Id == board.Id);
    }

    [Fact]
    public async Task GetBoard_AsNonMember_ReturnsForbid()
    {
        var board = await CreateBoardAsync("Private Board");
        var user = await CreateUserAsync($"nonmember_{Guid.NewGuid():N}");
        var userToken = await Helpers.LoginAsync(_client, user.Username, "pass");
        _client.SetBearer(userToken);
        var response = await _client.GetAsync($"/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetBoard_AsAdmin_ReturnsBoard()
    {
        var board = await CreateBoardAsync("Admin Visible Board");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.GetAsync($"/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateBoard_AsOwner_ReturnsUpdated()
    {
        var board = await CreateBoardAsync("Original Name");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PutAsJsonAsync($"/boards/{board.Id}", new UpdateBoardRequest("New Name", null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BoardResponse>();
        Assert.Equal("New Name", updated!.Name);
    }

    [Fact]
    public async Task UpdateBoard_AsNonOwner_ReturnsForbid()
    {
        var board = await CreateBoardAsync("Protected Board");
        var user = await CreateUserAsync($"member_{Guid.NewGuid():N}");

        // Add user as member
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        await _client.PostAsJsonAsync($"/boards/{board.Id}/members", new AddMemberRequest(user.Id));

        // Try to update as member (not owner)
        var userToken = await Helpers.LoginAsync(_client, user.Username, "pass");
        _client.SetBearer(userToken);
        var response = await _client.PutAsJsonAsync($"/boards/{board.Id}", new UpdateBoardRequest("Hacked Name", null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_AsOwner_ReturnsNoContent()
    {
        var board = await CreateBoardAsync("To Delete");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.DeleteAsync($"/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_AsOwner_AddsMember()
    {
        var board = await CreateBoardAsync("Member Board");
        var user = await CreateUserAsync($"addme_{Guid.NewGuid():N}");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync($"/boards/{board.Id}/members", new AddMemberRequest(user.Id));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify member can now access the board
        var userToken = await Helpers.LoginAsync(_client, user.Username, "pass");
        _client.SetBearer(userToken);
        var boardResponse = await _client.GetAsync($"/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.OK, boardResponse.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_AsOwner_RemovesMember()
    {
        var board = await CreateBoardAsync("Remove Member Board");
        var user = await CreateUserAsync($"removeme_{Guid.NewGuid():N}");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        await _client.PostAsJsonAsync($"/boards/{board.Id}/members", new AddMemberRequest(user.Id));
        var response = await _client.DeleteAsync($"/boards/{board.Id}/members/{user.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task GetBoard_NotFound_ReturnsNotFound()
    {
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.GetAsync("/boards/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateBoard_AsUser_ReturnsForbidden()
    {
        var user = await CreateUserAsync($"regularuser_{Guid.NewGuid():N}");
        var userToken = await Helpers.LoginAsync(_client, user.Username, "pass");
        _client.SetBearer(userToken);
        var response = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Should Fail", null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_WithNonExistentUser_ReturnsNotFound()
    {
        var board = await CreateBoardAsync("Member NotFound Board");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync($"/boards/{board.Id}/members", new AddMemberRequest(99999));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBoard_WithMembersColumnsAndCards_ReturnsNoContent()
    {
        var board = await CreateBoardAsync("Cascade Board");
        var user = await CreateUserAsync($"cascade_{Guid.NewGuid():N}");
        var token = await AdminTokenAsync();
        _client.SetBearer(token);
        await _client.PostAsJsonAsync($"/boards/{board.Id}/members", new AddMemberRequest(user.Id));
        var column = board.Columns.First();
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}/cards", new CreateCardRequest("A card", null));

        var response = await _client.DeleteAsync($"/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/boards/{board.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task GetBoards_AsAdmin_ReturnsAllBoards()
    {
        var adminToken = await AdminTokenAsync();
        _client.SetBearer(adminToken);
        var admin2Username = $"admin2_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(admin2Username, "pass1234", "admin"));
        var admin2Token = await Helpers.LoginAsync(_client, admin2Username, "pass1234");
        _client.SetBearer(admin2Token);
        var boardResponse = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Admin2 Exclusive Board", null));
        var admin2Board = (await boardResponse.Content.ReadFromJsonAsync<BoardResponse>())!;

        // First admin (not a member of admin2Board) should still see it
        _client.SetBearer(adminToken);
        var response = await _client.GetAsync("/boards");
        var boards = await response.Content.ReadFromJsonAsync<List<BoardSummaryResponse>>();
        Assert.Contains(boards!, b => b.Id == admin2Board.Id);
    }
}
