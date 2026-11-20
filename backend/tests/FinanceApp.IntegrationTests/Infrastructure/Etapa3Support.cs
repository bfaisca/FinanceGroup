using System.Net;
using System.Text.Json;
using FinanceApp.Application.CreditCards;
using FinanceApp.Application.Installments;
using FinanceApp.Application.Recurrences;
using FinanceApp.Application.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceApp.IntegrationTests.Infrastructure;

/// <summary>Helpers de cenário da Etapa 3 (cartões, parcelamentos e recorrências) sobre a API real.</summary>
public static class Etapa3Support
{
    public static async Task<CreditCardDto> CreateCardAsync(
        this FinancialHarness h,
        ApiUser user,
        string name = "Cartão",
        decimal limit = 5000m,
        int closingDay = 10,
        int dueDay = 20,
        Guid? accountId = null,
        string? institution = null)
    {
        var response = await user.Post("/api/v1/credit-cards", new { name, institution, creditLimit = limit, closingDay, dueDay, accountId });
        return await ApiUser.Read<CreditCardDto>(response, HttpStatusCode.Created);
    }

    public static object CardTxBody(string description, decimal amount, Guid categoryId, Guid cardId, DateOnly date, string? notes = null) =>
        new { description, type = "Expense", amount, categoryId, creditCardId = cardId, transactionDate = date, notes };

    public static async Task<TransactionDetailDto> CreateCardPurchaseAsync(
        this FinancialHarness h,
        ApiUser user,
        Guid cardId,
        string description,
        decimal amount,
        Guid categoryId,
        DateOnly date)
    {
        var response = await user.Post("/api/v1/transactions", CardTxBody(description, amount, categoryId, cardId, date));
        return await ApiUser.Read<TransactionDetailDto>(response, HttpStatusCode.Created);
    }

    public static object PlanBody(
        string description,
        decimal total,
        int count,
        DateOnly firstDate,
        Guid categoryId,
        Guid? accountId = null,
        Guid? cardId = null,
        string? paymentMethod = null,
        string? notes = null) =>
        new { description, totalAmount = total, installmentCount = count, firstDate, categoryId, accountId, creditCardId = cardId, paymentMethod, notes };

    public static async Task<InstallmentPlanDetailDto> CreatePlanAsync(
        this FinancialHarness h,
        ApiUser user,
        string description,
        decimal total,
        int count,
        DateOnly firstDate,
        Guid categoryId,
        Guid? accountId = null,
        Guid? cardId = null,
        string? paymentMethod = null)
    {
        var response = await user.Post("/api/v1/installment-plans", PlanBody(description, total, count, firstDate, categoryId, accountId, cardId, paymentMethod));
        return await ApiUser.Read<InstallmentPlanDetailDto>(response, HttpStatusCode.Created);
    }

    public static object RuleBody(
        string description,
        string type,
        decimal amount,
        Guid categoryId,
        string frequency,
        DateOnly start,
        DateOnly? end = null,
        int? interval = null,
        Guid? accountId = null,
        Guid? cardId = null,
        string? paymentMethod = null,
        string? notes = null) =>
        new { description, type, amount, categoryId, frequency, interval, startDate = start, endDate = end, accountId, creditCardId = cardId, paymentMethod, notes };

    public static object RuleUpdateBody(
        string description,
        decimal amount,
        Guid categoryId,
        string frequency,
        DateOnly? end = null,
        int? interval = null,
        Guid? accountId = null,
        Guid? cardId = null,
        string? paymentMethod = null,
        string? notes = null) =>
        new { description, amount, categoryId, frequency, interval, endDate = end, accountId, creditCardId = cardId, paymentMethod, notes };

    public static async Task<RecurrenceDto> CreateRuleAsync(
        this FinancialHarness h,
        ApiUser user,
        string description,
        string type,
        decimal amount,
        Guid categoryId,
        string frequency,
        DateOnly start,
        DateOnly? end = null,
        int? interval = null,
        Guid? accountId = null,
        Guid? cardId = null)
    {
        var response = await user.Post("/api/v1/recurrences", RuleBody(description, type, amount, categoryId, frequency, start, end, interval, accountId, cardId));
        return await ApiUser.Read<RecurrenceDto>(response, HttpStatusCode.Created);
    }

    /// <summary>Ocorrências geradas de uma regra, em ordem de data.</summary>
    public static async Task<List<TransactionDto>> OccurrencesAsync(this FinancialHarness h, ApiUser user, Guid ruleId)
    {
        var page = await h.ListAsync(user, $"recurrenceRuleId={ruleId}&pageSize=100&sort=transactionDate");
        var all = new List<TransactionDto>(page.Items);
        for (var p = 2; p <= page.TotalPages; p++)
        {
            all.AddRange((await h.ListAsync(user, $"recurrenceRuleId={ruleId}&pageSize=100&sort=transactionDate&page={p}")).Items);
        }

        return all;
    }

    /// <summary>Executa o gerador de recorrências (como o serviço em segundo plano) com o "hoje" do relógio de teste.</summary>
    public static async Task<int> RunGeneratorAsync(this FinancialHarness h, int horizonMonths = 12)
    {
        await using var scope = h.Factory.Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IRecurrenceGenerator>();
        return await generator.GenerateAllAsync(h.Factory.Clock.Today, horizonMonths, CancellationToken.None);
    }

    public static async Task<int> RunGeneratorForAsync(this FinancialHarness h, Guid ruleId, int horizonMonths = 12)
    {
        await using var scope = h.Factory.Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IRecurrenceGenerator>();
        return await generator.GenerateAsync(ruleId, h.Factory.Clock.Today, horizonMonths, CancellationToken.None);
    }

    public static bool HasError(this JsonElement problem, string field) =>
        problem.TryGetProperty("errors", out var errors) && errors.TryGetProperty(field, out _);
}
