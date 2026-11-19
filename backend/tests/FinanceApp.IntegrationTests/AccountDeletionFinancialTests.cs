using System.Net;
using FinanceApp.Domain.Categories;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

/// <summary>D-014: a exclusão da conta apaga os dados individuais na mesma transação e preserva os de outros usuários.</summary>
[Collection(ApiCollection.Name)]
public class AccountDeletionFinancialTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    private async Task SeedFinancialDataAsync(ApiUser user)
    {
        var account = await h.CreateAccountAsync(user, "Corrente", balance: 1000m, referenceDate: h.Today.AddDays(-5));
        await user.Post($"/api/v1/accounts/{account.Id}/balance-snapshots", new { balance = 1100m, referenceDate = h.Today });
        var custom = await h.CreateCategoryAsync(user, "Pets");
        await h.CreateCategoryAsync(user, "Ração", parent: custom.Id);
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var salary = await h.CategoryIdAsync(user, "Salário", CategoryType.Income);

        var paid = await h.CreateTxAsync(user, "Mercado", "Expense", 100m, food, h.Today, accountId: account.Id);
        await h.PayAsync(user, paid.Id, 100m, h.Today, account.Id);
        var partial = await h.CreateTxAsync(user, "Pets", "Expense", 80m, custom.Id, h.Today);
        await h.PayAsync(user, partial.Id, 30m, h.Today);
        var income = await h.CreateTxAsync(user, "Salário", "Income", 3000m, salary, h.Today, accountId: account.Id);
        await user.Post($"/api/v1/transactions/{income.Id}/cancel");
        await user.Put($"/api/v1/transactions/{paid.Id}", new { description = "Mercado 2", amount = 100m, categoryId = food, transactionDate = h.Today });
    }

    private Task<(int Accounts, int Snapshots, int Categories, int Transactions, int Payments, int Audit)> CountAsync(Guid userId) =>
        h.WithDbAsync(async db => (
            await db.Accounts.CountAsync(a => a.UserId == userId),
            await db.AccountBalanceSnapshots.CountAsync(s => db.Accounts.Any(a => a.Id == s.AccountId && a.UserId == userId)),
            await db.Categories.CountAsync(c => c.UserId == userId),
            await db.Transactions.CountAsync(t => t.CreatedByUserId == userId),
            await db.TransactionPayments.CountAsync(p => p.CreatedByUserId == userId),
            await db.TransactionAuditEntries.CountAsync(e => e.UserId == userId)));

    [Fact]
    public async Task DeleteAccount_RemovesAllIndividualFinancialData_AndKeepsOtherUsersData()
    {
        var doomed = await h.NewUserAsync();
        var bystander = await h.NewUserAsync();
        await SeedFinancialDataAsync(doomed);
        await SeedFinancialDataAsync(bystander);

        var before = await CountAsync(doomed.Id);
        var bystanderBefore = await CountAsync(bystander.Id);
        Assert.Equal((1, 2, 2, 3, 2, before.Audit), before);
        Assert.True(before.Audit >= 7);
        Assert.Equal(before, bystanderBefore);
        var defaultsBefore = await h.WithDbAsync(db => db.Categories.CountAsync(c => c.IsDefault));

        var response = await doomed.SendAsync(HttpMethod.Delete, "/api/v1/users/me", new { password = DefaultPassword });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal((0, 0, 0, 0, 0, 0), await CountAsync(doomed.Id));
        Assert.Equal(bystanderBefore, await CountAsync(bystander.Id));
        Assert.Equal(defaultsBefore, await h.WithDbAsync(db => db.Categories.CountAsync(c => c.IsDefault)));

        // O usuário foi anonimizado e desativado (D-014), mas o registro continua existindo.
        var user = await h.WithDbAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == doomed.Id));
        Assert.False(user.IsActive);
        Assert.Equal("Usuário removido", user.Name);

        // Os dados do outro usuário continuam acessíveis pela API.
        Assert.Equal(3, (await h.ListAsync(bystander)).TotalCount);
    }

    [Fact]
    public async Task DeleteAccount_WithWrongPassword_DeletesNothing()
    {
        var user = await h.NewUserAsync();
        await SeedFinancialDataAsync(user);
        var before = await CountAsync(user.Id);

        var response = await user.SendAsync(HttpMethod.Delete, "/api/v1/users/me", new { password = "Errada123" });

        await ReadProblemAsync(response, HttpStatusCode.BadRequest, "invalid_password");
        Assert.Equal(before, await CountAsync(user.Id));
    }

    [Fact]
    public async Task DeleteAccount_WithoutFinancialData_StillWorks()
    {
        var user = await h.NewUserAsync();
        var response = await user.SendAsync(HttpMethod.Delete, "/api/v1/users/me", new { password = DefaultPassword });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
