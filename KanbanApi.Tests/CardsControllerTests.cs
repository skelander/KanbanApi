using System.Net;
using System.Net.Http.Json;
using KanbanApi.Models;

namespace KanbanApi.Tests;

public class CardsControllerTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<BoardResponse> SetupAsync()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        var boardResponse = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Card Test Board", null));
        return (await boardResponse.Content.ReadFromJsonAsync<BoardResponse>())!;
    }

    [Fact]
    public async Task CreateCard_AsMember_ReturnsCreated()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var response = await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Card 1", "Description"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.Equal("Card 1", card!.Title);
        Assert.Equal("Description", card.Description);
        Assert.Equal(0, card.Position);
    }

    [Fact]
    public async Task CreateCard_HasInitialStateHistoryRecord()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var response = await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("History Card", null));
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        var history = card!.StateHistory.ToList();
        Assert.Single(history);
        Assert.Equal(column.Id, history[0].ColumnId);
        Assert.NotEqual(default, history[0].EnteredAt);
        Assert.Equal(DateOnly.FromDateTime(history[0].EnteredAt), history[0].EnteredDate);
        Assert.Null(history[0].ExitedAt);
        Assert.Null(history[0].ExitedDate);
    }

    [Fact]
    public async Task CreateMultipleCards_PositionsIncrement()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}/cards", new CreateCardRequest("A", null));
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}/cards", new CreateCardRequest("B", null));
        var response = await _client.PostAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}/cards", new CreateCardRequest("C", null));
        var card = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.Equal(2, card!.Position);
    }

    [Fact]
    public async Task GetCards_ReturnsSortedByPosition()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}/cards", new CreateCardRequest("First", null));
        await _client.PostAsJsonAsync($"/boards/{board.Id}/columns/{column.Id}/cards", new CreateCardRequest("Second", null));
        var response = await _client.GetAsync($"/boards/{board.Id}/columns/{column.Id}/cards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cards = await response.Content.ReadFromJsonAsync<List<CardResponse>>();
        Assert.Equal(2, cards!.Count);
        Assert.True(cards[0].Position <= cards[1].Position);
    }

    [Fact]
    public async Task UpdateCard_ChangeTitleAndDescription_ReturnsUpdated()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Original", null))).Content.ReadFromJsonAsync<CardResponse>();
        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards/{created!.Id}",
            new UpdateCardRequest("Updated Title", "New Desc", null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.Equal("Updated Title", updated!.Title);
        Assert.Equal("New Desc", updated.Description);
    }

    [Fact]
    public async Task DeleteCard_AsMember_ReturnsNoContent()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Delete Me", null))).Content.ReadFromJsonAsync<CardResponse>();
        var response = await _client.DeleteAsync($"/boards/{board.Id}/columns/{column.Id}/cards/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task MoveCard_ToAnotherColumn_UpdatesColumnId()
    {
        var board = await SetupAsync();
        var columns = board.Columns.ToList();
        var col1 = columns.First(c => c.IsBacklog);
        var col2 = columns.First(c => !c.IsBacklog);

        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{col1.Id}/cards",
            new CreateCardRequest("Move Me", null))).Content.ReadFromJsonAsync<CardResponse>();

        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{col1.Id}/cards/{created!.Id}/move",
            new MoveCardRequest(col2.Id, 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.Equal(col2.Id, moved!.ColumnId);
        Assert.Equal(0, moved.Position);
    }

    [Fact]
    public async Task MoveCard_RecordsStateHistory()
    {
        var board = await SetupAsync();
        var columns = board.Columns.ToList();
        var col1 = columns[0]; // Backlog
        var col2 = columns[1]; // Todo
        var col3 = columns[2]; // Reqs

        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{col1.Id}/cards",
            new CreateCardRequest("Flow Card", null))).Content.ReadFromJsonAsync<CardResponse>();

        // Move: Backlog → Todo
        await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{col1.Id}/cards/{created!.Id}/move",
            new MoveCardRequest(col2.Id, 0));

        // Move: Todo → Reqs
        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{col2.Id}/cards/{created.Id}/move",
            new MoveCardRequest(col3.Id, 0));

        var moved = await response.Content.ReadFromJsonAsync<CardResponse>();
        var history = moved!.StateHistory.ToList();

        Assert.Equal(3, history.Count);
        Assert.Equal(col1.Name, history[0].ColumnName);
        Assert.NotNull(history[0].ExitedAt);
        Assert.NotNull(history[0].ExitedDate);
        Assert.Equal(DateOnly.FromDateTime(history[0].ExitedAt!.Value), history[0].ExitedDate);
        Assert.Equal(col2.Name, history[1].ColumnName);
        Assert.NotNull(history[1].ExitedAt);
        Assert.NotNull(history[1].ExitedDate);
        Assert.Equal(col3.Name, history[2].ColumnName);
        Assert.Null(history[2].ExitedAt);
        Assert.Null(history[2].ExitedDate);
    }

    [Fact]
    public async Task MoveCard_ToInvalidColumn_ReturnsNotFound()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Card", null))).Content.ReadFromJsonAsync<CardResponse>();
        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards/{created!.Id}/move",
            new MoveCardRequest(99999, 0));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCards_AsNonMember_ReturnsForbid()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var outsiderUsername = $"outsider_{Guid.NewGuid():N}";
        var adminToken = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(adminToken);
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(outsiderUsername, "pass1234", "user"));
        var outsiderToken = await Helpers.LoginAsync(_client, outsiderUsername, "pass1234");
        _client.SetBearer(outsiderToken);
        var response = await _client.GetAsync($"/boards/{board.Id}/columns/{column.Id}/cards");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCards_ColumnNotFound_ReturnsNotFound()
    {
        var board = await SetupAsync();
        var response = await _client.GetAsync($"/boards/{board.Id}/columns/99999/cards");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetCards_AsAdmin_CanAccessAnyBoard()
    {
        // Create a second admin who owns a board (first admin is not a member)
        var adminToken = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(adminToken);
        var admin2Username = $"admin2_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest(admin2Username, "pass1234", "admin"));
        var admin2Token = await Helpers.LoginAsync(_client, admin2Username, "pass1234");
        _client.SetBearer(admin2Token);
        var boardResponse = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Admin Bypass Board", null));
        var board = (await boardResponse.Content.ReadFromJsonAsync<BoardResponse>())!;
        var column = board.Columns.First();

        // First admin (not a member) should bypass the membership check
        _client.SetBearer(adminToken);
        var response = await _client.GetAsync($"/boards/{board.Id}/columns/{column.Id}/cards");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCard_ChangePosition_ReturnsUpdatedPosition()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Card", null))).Content.ReadFromJsonAsync<CardResponse>();
        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards/{created!.Id}",
            new UpdateCardRequest(null, null, 5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.Equal(5, updated!.Position);
    }

    [Fact]
    public async Task MoveCard_ToSameColumn_ReturnsOk()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var created = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Same Column Card", null))).Content.ReadFromJsonAsync<CardResponse>();
        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards/{created!.Id}/move",
            new MoveCardRequest(column.Id, 0));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var moved = await response.Content.ReadFromJsonAsync<CardResponse>();
        Assert.Equal(column.Id, moved!.ColumnId);
    }

    [Fact]
    public async Task MoveCard_ToColumnOnDifferentBoard_ReturnsNotFound()
    {
        var board = await SetupAsync();
        var column = board.Columns.First(c => c.IsBacklog);
        var card = await (await _client.PostAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards",
            new CreateCardRequest("Cross Board Card", null))).Content.ReadFromJsonAsync<CardResponse>();

        // Create a second board and grab one of its default columns
        var board2Response = await _client.PostAsJsonAsync("/boards", new CreateBoardRequest("Other Board", null));
        var board2 = (await board2Response.Content.ReadFromJsonAsync<BoardResponse>())!;
        var otherColumn = board2.Columns.First();

        // Moving to a column that belongs to a different board should return NotFound
        var response = await _client.PutAsJsonAsync(
            $"/boards/{board.Id}/columns/{column.Id}/cards/{card!.Id}/move",
            new MoveCardRequest(otherColumn.Id, 0));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
