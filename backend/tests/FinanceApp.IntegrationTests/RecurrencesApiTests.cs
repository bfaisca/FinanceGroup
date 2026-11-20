using System.Net;
using FinanceApp.Application.Recurrences;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Recurrences;
using FinanceApp.Domain.Transactions;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;
using static FinanceApp.IntegrationTests.Infrastructure.Etapa3Support;

namespace FinanceApp.IntegrationTests;

/// <summary>Recorrências (contrato §13, D-027). "Hoje" fixado em 2026-06-15; horizonte padrão de 12 meses.</summary>
[Collection(ApiCollection.Name)]
public sealed class RecurrencesApiTests : IDisposable
{
    private static readonly DateOnly FixedToday = new(2026, 6, 15);

    private readonly ApiFactory factory;
    private readonly FinancialHarness h;

    public RecurrencesApiTests(ApiFactory factory)
    {
        this.factory = factory;
        h = new FinancialHarness(factory);
        factory.Clock.TodayOverride = FixedToday;
    }

    public void Dispose() => factory.Clock.TodayOverride = null;

    private static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    [Fact]
    public async Task Create_FillsHorizon_FirstOccurrencePlanned()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");

        var rule = await h.CreateRuleAsync(user, "Streaming", "Expense", 39.9m, category, "Monthly", D(6, 20));

        var occ = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal(12, occ.Count);
        Assert.Equal(D(6, 20), occ[0].TransactionDate);
        Assert.Equal(D(5, 20, 2027), occ[^1].TransactionDate);
        Assert.All(occ, o =>
        {
            Assert.Equal(TransactionStatus.Planned, o.Status);
            Assert.Equal(39.9m, o.Amount);
            Assert.Equal(rule.Id, o.RecurrenceRuleId);
        });
        Assert.Equal(12, rule.GeneratedCount);
        Assert.Equal(D(6, 20), rule.NextOccurrenceDate);
        Assert.True(rule.IsActive);
        Assert.Equal(1, rule.Interval);
    }

    [Fact]
    public async Task Create_WithPastStart_GeneratesPendingAndOverdueOccurrences()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Moradia");

        var rule = await h.CreateRuleAsync(user, "Aluguel", "Expense", 1000m, category, "Monthly", D(4, 5), end: D(8, 5));

        var occ = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal([D(4, 5), D(5, 5), D(6, 5), D(7, 5), D(8, 5)], occ.Select(o => o.TransactionDate));
        Assert.Equal(
            [TransactionStatus.Overdue, TransactionStatus.Overdue, TransactionStatus.Overdue, TransactionStatus.Planned, TransactionStatus.Planned],
            occ.Select(o => o.Status));
    }

    [Fact]
    public async Task Create_MonthEnd_ClampsWithoutDrift()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Moradia");

        var rule = await h.CreateRuleAsync(user, "Fim de mês", "Expense", 10m, category, "Monthly", D(1, 31), end: D(6, 30));

        var occ = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal([D(1, 31), D(2, 28), D(3, 31), D(4, 30), D(5, 31), D(6, 30)], occ.Select(o => o.TransactionDate));
    }

    [Fact]
    public async Task Generator_RunTwice_DoesNotDuplicate_AndClockAdvanceGeneratesMore()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var rule = await h.CreateRuleAsync(user, "Semanal", "Expense", 5m, category, "Weekly", D(6, 16));
        var before = (await h.OccurrencesAsync(user, rule.Id)).Count;

        Assert.Equal(0, await h.RunGeneratorAsync());
        Assert.Equal(before, (await h.OccurrencesAsync(user, rule.Id)).Count);

        factory.Clock.TodayOverride = D(7, 15);
        var created = await h.RunGeneratorForAsync(rule.Id);

        Assert.True(created > 0);
        var after = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal(before + created, after.Count);
        Assert.Equal(after.Count, after.Select(o => o.TransactionDate).Distinct().Count());
    }

    [Fact]
    public async Task Generator_SimultaneousRuns_NeverDuplicate()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var rule = await h.CreateRuleAsync(user, "Concorrente", "Expense", 5m, category, "Weekly", D(6, 16));
        var expected = (await h.OccurrencesAsync(user, rule.Id)).Count;
        await h.WithDbAsync(db => db.Transactions.Where(t => t.RecurrenceRuleId == rule.Id).ExecuteDeleteAsync());

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => h.RunGeneratorForAsync(rule.Id))));

        var after = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal(expected, after.Count);
        Assert.Equal(expected, after.Select(o => o.TransactionDate).Distinct().Count());
        Assert.Equal(expected, results.Sum());
    }

    [Fact]
    public async Task Update_Regenerates_KeepingPaidEditedCanceledAndPastOccurrences()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var rule = await h.CreateRuleAsync(user, "Academia", "Expense", 100m, category, "Monthly", D(5, 20), end: D(11, 20));
        var occ = await h.OccurrencesAsync(user, rule.Id); // 05-20 .. 11-20 (7)
        Assert.Equal(7, occ.Count);
        await h.PayAsync(user, occ[2].Id, 100m, D(6, 10));                       // 07-20 paga
        var edited = await user.Put($"/api/v1/transactions/{occ[3].Id}", new { description = "Academia (ajustada)", amount = 120m, categoryId = category, transactionDate = occ[3].TransactionDate });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);                       // 08-20 editada
        await user.Post($"/api/v1/transactions/{occ[4].Id}/cancel");              // 09-20 cancelada

        var updated = await user.Put($"/api/v1/recurrences/{rule.Id}", RuleUpdateBody("Academia nova", 150m, category, "Monthly", end: D(10, 20)));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var after = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal(new[] { D(5, 20), D(6, 20), D(7, 20), D(8, 20), D(9, 20), D(10, 20) }, after.Select(o => o.TransactionDate));
        Assert.Equal(100m, after[0].Amount);                // passada: intacta
        Assert.Equal(150m, after[1].Amount);                // 06-20 é futura (hoje = 06-15) e aberta: regenerada com a nova regra
        Assert.Equal(100m, after[2].Amount);                // paga: preservada
        Assert.Equal(120m, after[3].Amount);                // editada manualmente: preservada
        Assert.Equal(TransactionStatus.Canceled, after[4].Status); // cancelada: preservada
        Assert.Equal(150m, after[5].Amount);                // 10-20 regenerada
        Assert.Equal(D(10, 20), (await user.GetOk<RecurrenceDto>($"/api/v1/recurrences/{rule.Id}")).EndDate);
    }

    [Fact]
    public async Task Update_WithoutChanges_KeepsOccurrenceIds_AndChangeRegeneratesThem()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var rule = await h.CreateRuleAsync(user, "Academia", "Expense", 100m, category, "Monthly", D(5, 20), end: D(11, 20));
        var before = await h.OccurrencesAsync(user, rule.Id);

        var same = await user.Put($"/api/v1/recurrences/{rule.Id}", RuleUpdateBody("Academia", 100m, category, "Monthly", end: D(11, 20)));

        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        var unchanged = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal(before.Select(o => o.Id), unchanged.Select(o => o.Id));   // sem mudança: ids estáveis

        var changed = await user.Put($"/api/v1/recurrences/{rule.Id}", RuleUpdateBody("Academia", 110m, category, "Monthly", end: D(11, 20)));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var regenerated = await h.OccurrencesAsync(user, rule.Id);
        Assert.Equal(before.Count, regenerated.Count);
        Assert.Equal(before[0].Id, regenerated[0].Id);                          // passada: intacta
        Assert.NotEqual(before[2].Id, regenerated[2].Id);                       // futura aberta: regenerada
        Assert.Equal(110m, regenerated[2].Amount);
    }

    [Fact]
    public async Task Delete_OfOccurrence_Returns422_AndPutMarksManualEdit()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var rule = await h.CreateRuleAsync(user, "Seguro", "Expense", 50m, category, "Monthly", D(7, 1), end: D(9, 1));
        var occ = await h.OccurrencesAsync(user, rule.Id);

        await ReadProblemAsync(await user.Delete($"/api/v1/transactions/{occ[0].Id}"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        var put = await user.Put($"/api/v1/transactions/{occ[0].Id}", new { description = "Seguro (manual)", amount = 55m, categoryId = category, transactionDate = occ[0].TransactionDate });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.True(await h.WithDbAsync(db => db.Transactions.Where(t => t.Id == occ[0].Id).Select(t => t.IsManuallyEdited).SingleAsync()));
        Assert.False(await h.WithDbAsync(db => db.Transactions.Where(t => t.Id == occ[1].Id).Select(t => t.IsManuallyEdited).SingleAsync()));
    }

    [Fact]
    public async Task Deactivate_RemovesFutureOpenOccurrences_AndActivateRegeneratesThem()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var rule = await h.CreateRuleAsync(user, "Revista", "Expense", 20m, category, "Monthly", D(6, 10), end: D(10, 10));
        var occ = await h.OccurrencesAsync(user, rule.Id); // 06-10 (passada), 07-10.. (futuras)
        await h.PayAsync(user, occ[1].Id, 20m, D(6, 12)); // 07-10 paga: preservada

        var deactivated = await ApiUser.Read<RecurrenceDto>(await user.Post($"/api/v1/recurrences/{rule.Id}/deactivate"));

        Assert.False(deactivated.IsActive);
        Assert.Equal([D(6, 10), D(7, 10)], (await h.OccurrencesAsync(user, rule.Id)).Select(o => o.TransactionDate));
        Assert.Equal(0, await h.RunGeneratorForAsync(rule.Id)); // inativa: gerador não gera

        var activated = await ApiUser.Read<RecurrenceDto>(await user.Post($"/api/v1/recurrences/{rule.Id}/activate"));

        Assert.True(activated.IsActive);
        Assert.Equal([D(6, 10), D(7, 10), D(8, 10), D(9, 10), D(10, 10)], (await h.OccurrencesAsync(user, rule.Id)).Select(o => o.TransactionDate));
    }

    [Fact]
    public async Task CardRule_OccurrencesUseInvoiceDueDate_AndInactiveCardIsSkipped()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");
        var card = await h.CreateCardAsync(user, "Visa", closingDay: 10, dueDay: 20);
        var rule = await h.CreateRuleAsync(user, "Cloud", "Expense", 30m, category, "Monthly", D(6, 5), end: D(8, 15), cardId: card.Id);

        var occ = await h.OccurrencesAsync(user, rule.Id);

        Assert.Equal([D(6, 5), D(7, 5), D(8, 5)], occ.Select(o => o.TransactionDate));
        Assert.Equal([D(6, 20), D(7, 20), D(8, 20)], occ.Select(o => o.DueDate));
        Assert.All(occ, o => Assert.Equal(card.Id, o.CreditCardId));

        await user.Post($"/api/v1/credit-cards/{card.Id}/deactivate");
        factory.Clock.TodayOverride = D(7, 1);
        var firstDate = D(6, 5);
        await h.WithDbAsync(db => db.Transactions.Where(t => t.RecurrenceRuleId == rule.Id && t.TransactionDate > firstDate).ExecuteDeleteAsync());
        Assert.Equal(0, await h.RunGeneratorForAsync(rule.Id));
    }

    [Fact]
    public async Task Create_Validation_ReturnsFieldErrors()
    {
        var user = await h.NewUserAsync();
        var category = await h.CategoryIdAsync(user, "Assinaturas");

        var p1 = await ReadProblemAsync(await user.Post("/api/v1/recurrences", RuleBody("", "Expense", 0m, category, "Monthly", D(6, 1), interval: 0)), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(p1.HasError("description"));
        Assert.True(p1.HasError("amount"));
        Assert.True(p1.HasError("interval"));

        var p2 = await ReadProblemAsync(await user.Post("/api/v1/recurrences", RuleBody("X", "Expense", 10m, category, "Monthly", D(6, 1), end: D(5, 1))), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(p2.HasError("endDate"));

        var p3 = await ReadProblemAsync(await user.Post("/api/v1/recurrences", RuleBody("X", "Income", 10m, category, "Monthly", D(6, 1), cardId: Guid.NewGuid())), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(p3.HasError("creditCardId"));
    }

    [Fact]
    public async Task Isolation_OtherUsersRule_Returns404_AndForeignRefsReturn400()
    {
        var owner = await h.NewUserAsync("Dona");
        var other = await h.NewUserAsync("Outro");
        var ownerCategory = (await h.CreateCategoryAsync(owner, "Só do dono")).Id;
        var otherCategory = await h.CategoryIdAsync(other, "Assinaturas");
        var rule = await h.CreateRuleAsync(owner, "Privada", "Expense", 10m, ownerCategory, "Monthly", D(7, 1), end: D(8, 1));
        var foreignCard = await h.CreateCardAsync(owner, "Do dono");

        await ReadProblemAsync(await other.Get($"/api/v1/recurrences/{rule.Id}"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await other.Put($"/api/v1/recurrences/{rule.Id}", RuleUpdateBody("X", 1m, otherCategory, "Monthly")), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await other.Post($"/api/v1/recurrences/{rule.Id}/deactivate"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await other.Post($"/api/v1/recurrences/{rule.Id}/activate"), HttpStatusCode.NotFound, "not_found");
        Assert.Empty(await other.GetOk<List<RecurrenceDto>>("/api/v1/recurrences?includeInactive=true"));
        Assert.Empty((await h.ListAsync(other, $"recurrenceRuleId={rule.Id}")).Items);
        var bad = await ReadProblemAsync(await other.Post("/api/v1/recurrences", RuleBody("X", "Expense", 1m, otherCategory, "Monthly", D(7, 1), cardId: foreignCard.Id)), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(bad.HasError("creditCardId"));
        var badCat = await ReadProblemAsync(await other.Post("/api/v1/recurrences", RuleBody("X", "Expense", 1m, ownerCategory, "Monthly", D(7, 1))), HttpStatusCode.BadRequest, "validation_error");
        Assert.True(badCat.HasError("categoryId"));
    }
}
