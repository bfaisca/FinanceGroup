using System.Net;
using System.Net.Http.Json;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RateLimitingTests(ApiFactory factory)
{
    [Fact]
    public async Task AuthEndpoints_ExceedingLimit_Returns429ProblemDetails()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Enabled"] = "true",
            ["RateLimiting:Auth:PermitLimit"] = "2",
            ["RateLimiting:Auth:WindowSeconds"] = "60",
        })));
        using var client = limited.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

        for (var i = 0; i < 2; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = UniqueEmail() });
            Assert.Equal(HttpStatusCode.Accepted, ok.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = UniqueEmail() });

        await ReadProblemAsync(limitedResponse, HttpStatusCode.TooManyRequests, "too_many_requests");

        // Health não é limitado.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }
}
