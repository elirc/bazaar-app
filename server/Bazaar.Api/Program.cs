var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => "Bazaar API");
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "bazaar-api" }));

app.Run();

// Exposed so WebApplicationFactory-based integration tests can reference the entry point.
public partial class Program { }
