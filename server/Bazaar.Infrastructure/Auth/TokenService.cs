using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bazaar.Domain.Customers;

namespace Bazaar.Infrastructure.Auth;

public sealed record AuthToken(string AccessToken, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AuthToken CreateToken(Customer customer);
}

/// <summary>
/// Issues a compact HS256 JWT by hand (no external token library). The token carries the standard
/// registered claims plus <c>email</c>/<c>role</c>, and is verified by the JwtBearer middleware in
/// the API using the same symmetric signing key.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly AuthOptions _options;

    public JwtTokenService(AuthOptions options) => _options = options;

    public AuthToken CreateToken(Customer customer)
    {
        var issuedAt = DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["sub"] = customer.Id.ToString(),
            ["email"] = customer.Email,
            // Standard role claim URI so ASP.NET role checks work without custom RoleClaimType mapping.
            [ClaimTypes.Role] = customer.Role.ToString(),
            ["name"] = customer.DisplayName,
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["iat"] = issuedAt.ToUnixTimeSeconds(),
            ["nbf"] = issuedAt.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions));
        var encodedPayload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));

        return new AuthToken($"{signingInput}.{Base64Url(signature)}", expiresAt);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
