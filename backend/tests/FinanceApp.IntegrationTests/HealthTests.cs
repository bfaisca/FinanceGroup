using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceApp.IntegrationTests.Infrastructure;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_ReturnsHealthyWithDatabaseCheck()
    {
        using var client = factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
    }
}
