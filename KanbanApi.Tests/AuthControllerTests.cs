using System.Net;
using System.Net.Http.Json;
using KanbanApi.Models;

namespace KanbanApi.Tests;

public class AuthControllerTests(KanbanApiFactory factory) : IClassFixture<KanbanApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "admin"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(result?.Token);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "wrong"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new LoginRequest("nobody", "pass"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/auth/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsUserList()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        var response = await _client.GetAsync("/auth/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<UserResponse>>();
        Assert.NotEmpty(users!);
    }

    [Fact]
    public async Task CreateUser_AsAdmin_CreatesUser()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        var response = await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest("newuser", "pass", "user"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("newuser", user!.Username);
        Assert.Equal("user", user.Role);
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_ReturnsConflict()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest("dupuser", "pass", "user"));
        var response = await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest("dupuser", "pass", "user"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_DeletesUser()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        var created = await _client.PostAsJsonAsync("/auth/users", new CreateUserRequest("todelete", "pass", "user"));
        var user = await created.Content.ReadFromJsonAsync<UserResponse>();
        var response = await _client.DeleteAsync($"/auth/users/{user!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_NotFound_ReturnsNotFound()
    {
        var token = await Helpers.LoginAsync(_client, "admin", "admin");
        _client.SetBearer(token);
        var response = await _client.DeleteAsync("/auth/users/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
