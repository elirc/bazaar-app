using System.ComponentModel.DataAnnotations;

namespace Bazaar.Api.Contracts;

public sealed record CustomerDto(
    Guid Id,
    string Email,
    string? FirstName,
    string? LastName,
    string Role);

public sealed record AuthResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    CustomerDto Customer);

public sealed record RegisterRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string? Email { get; init; }

    [Required, StringLength(200, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    public string? Password { get; init; }

    [StringLength(120)]
    public string? FirstName { get; init; }

    [StringLength(120)]
    public string? LastName { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(320)]
    public string? Email { get; init; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string? Password { get; init; }
}
