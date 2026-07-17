namespace Bazaar.Infrastructure.Auth;

/// <summary>Configuration for issuing and validating JWT access tokens. Bound from the "Auth" config section.</summary>
public sealed class AuthOptions
{
    /// <summary>HMAC-SHA256 signing key. Must be at least 32 bytes. Override in production configuration.</summary>
    public string SigningKey { get; set; } =
        "bazaar-development-signing-key-change-me-in-production-0123456789";

    public string Issuer { get; set; } = "bazaar";
    public string Audience { get; set; } = "bazaar-client";
    public int AccessTokenMinutes { get; set; } = 120;
}
