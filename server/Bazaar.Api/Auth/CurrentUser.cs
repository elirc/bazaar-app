using System.Security.Claims;

namespace Bazaar.Api.Auth;

/// <summary>Reads the Bazaar-specific claims off the authenticated principal.</summary>
public static class CurrentUser
{
    /// <summary>The signed-in customer id (the JWT <c>sub</c> claim), or null when the request is anonymous.</summary>
    public static Guid? GetCustomerId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
