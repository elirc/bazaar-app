using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bazaar.Api.Contracts;
using Bazaar.Infrastructure.Auth;

namespace Bazaar.Tests.TestSupport;

/// <summary>Helpers for obtaining and attaching bearer tokens in integration tests.</summary>
public static class AuthTestExtensions
{
    public static async Task<AuthResponse> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static async Task<AuthResponse> RegisterAsync(
        this HttpClient client, string email, string password, string? firstName = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = password, FirstName = firstName });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    public static void UseBearer(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    /// <summary>Log in as the seeded development admin and attach the bearer token to the client.</summary>
    public static async Task AuthenticateAdminAsync(this HttpClient client)
    {
        var auth = await client.LoginAsync(AccountSeeder.AdminEmail, AccountSeeder.AdminPassword);
        client.UseBearer(auth.Token);
    }
}
