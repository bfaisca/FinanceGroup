using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceApp.Application.Accounts;
using FinanceApp.Application.Categories;
using FinanceApp.Application.Common;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.IntegrationTests.Infrastructure;

/// <summary>Usuário autenticado de teste com helpers HTTP.</summary>
public sealed class ApiUser(HttpClient client, Session session)
{
    public static readonly JsonSerializerOptions Json = CreateJsonOptions();

    public Guid Id => session.Auth.User.Id;

    public Session Session => session;

    public string AccessToken => session.Auth.AccessToken;

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Auth.AccessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return client.SendAsync(request);
    }

    public Task<HttpResponseMessage> Get(string url) => SendAsync(HttpMethod.Get, url);

    public Task<HttpResponseMessage> Post(string url, object? body = null) => SendAsync(HttpMethod.Post, url, body);

    public Task<HttpResponseMessage> Put(string url, object body) => SendAsync(HttpMethod.Put, url, body);

    public Task<HttpResponseMessage> Delete(string url) => SendAsync(HttpMethod.Delete, url);

    public static async Task<T> Read<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        if (response.StatusCode != expected)
        {
            Assert.Fail($"Esperado {(int)expected} mas recebeu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task<T> GetOk<T>(string url) => await Read<T>(await Get(url));

    public async Task<JsonElement> GetJson(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await Get(url);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    public static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>Atalhos para montar cenários financeiros pela API real.</summary>
public sealed class FinancialHarness(ApiFactory factory)
{
    public HttpClient Client { get; } = factory.CreateApiClient();

    public ApiFactory Factory => factory;

    public DateOnly Today => factory.Clock.Today;

    public async Task<ApiUser> NewUserAsync(string name = "Maria Silva")
    {
        var session = await Client.RegisterAsync(name: name);
        return new ApiUser(Client, session);
    }

    public async Task<AccountDto> CreateAccountAsync(ApiUser user, string name = "Conta", decimal? balance = null, DateOnly? referenceDate = null, string type = "Checking")
    {
        var response = await user.Post("/api/v1/accounts", new { name, type, initialBalance = balance, balanceReferenceDate = referenceDate });
        return await ApiUser.Read<AccountDto>(response, HttpStatusCode.Created);
    }

    public async Task<Guid> CategoryIdAsync(ApiUser user, string name, CategoryType type = CategoryType.Expense)
    {
        var all = await user.GetOk<List<CategoryDto>>($"/api/v1/categories?type={type}&includeInactive=true");
        return all.Single(c => c.Name == name && c.ParentCategoryId is null).Id;
    }

    public async Task<CategoryDto> CreateCategoryAsync(ApiUser user, string name, CategoryType type = CategoryType.Expense, Guid? parent = null, string? color = null, string? icon = null)
    {
        var response = await user.Post("/api/v1/categories", new { name, type = type.ToString(), parentCategoryId = parent, color, icon });
        return await ApiUser.Read<CategoryDto>(response, HttpStatusCode.Created);
    }

    public static object TxBody(
        string description,
        string type,
        decimal amount,
        Guid categoryId,
        DateOnly date,
        DateOnly? due = null,
        Guid? accountId = null,
        string? status = null,
        string? paymentMethod = null,
        string? notes = null) =>
        new { description, type, amount, categoryId, accountId, paymentMethod, status, transactionDate = date, dueDate = due, notes };

    public async Task<TransactionDetailDto> CreateTxAsync(
        ApiUser user,
        string description,
        string type,
        decimal amount,
        Guid categoryId,
        DateOnly date,
        DateOnly? due = null,
        Guid? accountId = null,
        string? status = null)
    {
        var response = await user.Post("/api/v1/transactions", TxBody(description, type, amount, categoryId, date, due, accountId, status));
        return await ApiUser.Read<TransactionDetailDto>(response, HttpStatusCode.Created);
    }

    public async Task<TransactionDetailDto> PayAsync(ApiUser user, Guid transactionId, decimal amount, DateOnly date, Guid? accountId = null)
    {
        var response = await user.Post($"/api/v1/transactions/{transactionId}/payments", new { amount, paymentDate = date, accountId });
        return await ApiUser.Read<TransactionDetailDto>(response, HttpStatusCode.Created);
    }

    public Task<PagedResult<TransactionDto>> ListAsync(ApiUser user, string query = "") =>
        user.GetOk<PagedResult<TransactionDto>>("/api/v1/transactions" + (query.Length == 0 ? string.Empty : "?" + query));

    /// <summary>Executa uma consulta direta ao banco de testes (para verificar efeitos que a API não expõe).</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
