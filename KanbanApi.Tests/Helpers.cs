using System.Net.Http.Headers;
using System.Net.Http.Json;
using KanbanApi.Models;

namespace KanbanApi.Tests;

public static class Helpers
{
    public static async Task<string> LoginAsync(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result!.Token;
    }

    public static void SetBearer(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
