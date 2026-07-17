using System.Net;
using System.Net.Http.Json;
using Bazaar.Tests.TestSupport;

namespace Bazaar.Tests;

public class HealthEndpointTests : IClassFixture<BazaarApiFactory>
{
    private readonly BazaarApiFactory _factory;

    public HealthEndpointTests(BazaarApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_ok_status()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
        Assert.Equal("bazaar-api", body.Service);
    }

    private sealed record HealthResponse(string Status, string Service);
}
