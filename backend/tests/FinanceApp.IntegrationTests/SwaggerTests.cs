using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SwaggerTests(ApiFactory factory)
{
    [Fact]
    public void OpenApiDocument_DescribesAllStage2Routes()
    {
        using var scope = factory.Services.CreateScope();
        var document = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        var paths = document.Paths.Keys.ToHashSet();
        foreach (var path in new[]
                 {
                     "/api/v1/accounts", "/api/v1/accounts/{id}", "/api/v1/accounts/{id}/deactivate", "/api/v1/accounts/{id}/activate",
                     "/api/v1/accounts/{id}/balance-snapshots",
                     "/api/v1/categories", "/api/v1/categories/{id}", "/api/v1/categories/{id}/deactivate", "/api/v1/categories/{id}/activate",
                     "/api/v1/transactions", "/api/v1/transactions/{id}", "/api/v1/transactions/{id}/cancel", "/api/v1/transactions/{id}/reopen",
                     "/api/v1/transactions/{id}/history", "/api/v1/transactions/{id}/payments", "/api/v1/transactions/{id}/payments/{paymentId}",
                     "/api/v1/dashboard",
                 })
        {
            Assert.Contains(path, paths);
        }
    }
}
