using System.Security.Claims;
using Bazaar.Api.Auth;
using Bazaar.Api.Contracts;
using Bazaar.Api.Validation;
using Bazaar.Domain;
using Bazaar.Domain.Customers;
using Bazaar.Infrastructure.Auth;
using Bazaar.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bazaar.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapGet("/me", Me).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Register(
        BazaarDbContext db, IPasswordHasher hasher, ITokenService tokens, RegisterRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var email = request.Email!.Trim().ToLowerInvariant();
        if (await db.Customers.AnyAsync(c => c.Email == email, ct))
            return Results.Problem(
                "An account with that email already exists.",
                statusCode: StatusCodes.Status409Conflict, title: "Email already registered");

        var customer = new Customer
        {
            Email = email,
            FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName!.Trim(),
            LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName!.Trim(),
            Role = CustomerRole.Customer,
            PasswordHash = hasher.Hash(request.Password!),
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/auth/me", Issue(customer, tokens));
    }

    private static async Task<IResult> Login(
        BazaarDbContext db, IPasswordHasher hasher, ITokenService tokens, LoginRequest request, CancellationToken ct)
    {
        if (!RequestValidation.TryValidate(request, out var errors))
            return Results.ValidationProblem(errors);

        var email = request.Email!.Trim().ToLowerInvariant();
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (customer is null || !hasher.Verify(request.Password!, customer.PasswordHash))
            return Results.Problem(
                "Invalid email or password.",
                statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

        return Results.Ok(Issue(customer, tokens));
    }

    private static async Task<IResult> Me(BazaarDbContext db, ClaimsPrincipal principal, CancellationToken ct)
    {
        var id = principal.GetCustomerId();
        if (id is null) return Results.Unauthorized();

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return customer is null ? Results.NotFound() : Results.Ok(ToDto(customer));
    }

    private static AuthResponse Issue(Customer customer, ITokenService tokens)
    {
        var token = tokens.CreateToken(customer);
        return new AuthResponse(token.AccessToken, token.ExpiresAt, ToDto(customer));
    }

    private static CustomerDto ToDto(Customer c) =>
        new(c.Id, c.Email, c.FirstName, c.LastName, c.Role.ToString());
}
