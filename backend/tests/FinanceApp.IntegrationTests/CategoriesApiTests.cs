using System.Net;
using System.Net.Http.Json;
using FinanceApp.Application.Categories;
using FinanceApp.Application.Transactions;
using FinanceApp.Domain.Categories;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static FinanceApp.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace FinanceApp.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CategoriesApiTests(ApiFactory factory)
{
    private readonly FinancialHarness h = new(factory);

    [Fact]
    public async Task RequiresAuthentication() =>
        await ReadProblemAsync(await h.Client.GetAsync("/api/v1/categories"), HttpStatusCode.Unauthorized, "unauthorized");

    [Fact]
    public async Task List_ContainsSeededDefaults_WithFixedIds_AndContractShape()
    {
        var user = await h.NewUserAsync();

        var json = await user.GetJson("/api/v1/categories");
        var first = json[0];
        Assert.Equal(
            ["color", "icon", "id", "isActive", "isDefault", "name", "parentCategoryId", "type"],
            first.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        var all = await user.GetOk<List<CategoryDto>>("/api/v1/categories");
        var defaults = all.Where(c => c.IsDefault).ToList();
        Assert.Equal(14, defaults.Count);
        Assert.All(defaults, c => { Assert.True(c.IsActive); Assert.Null(c.ParentCategoryId); Assert.NotNull(c.Icon); Assert.NotNull(c.Color); });
        Assert.Equal(
            new[] { "Alimentação", "Saúde", "Educação", "Lazer", "Assinaturas", "Moradia", "Transporte", "Compras", "Outros" }.Order(StringComparer.Ordinal),
            defaults.Where(c => c.Type == CategoryType.Expense).Select(c => c.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "Salário", "Freelance", "Investimentos", "Reembolsos", "Outros" }.Order(StringComparer.Ordinal),
            defaults.Where(c => c.Type == CategoryType.Income).Select(c => c.Name).Order(StringComparer.Ordinal));
        Assert.Equal(
            DefaultCategories.All.Select(c => c.Id).Order(),
            defaults.Select(c => c.Id).Order());

        Assert.Equal(9, (await user.GetOk<List<CategoryDto>>("/api/v1/categories?type=Expense")).Count);
        Assert.Equal(5, (await user.GetOk<List<CategoryDto>>("/api/v1/categories?type=Income")).Count);
    }

    [Fact]
    public async Task Seed_IsIdempotent_StartingTheApplicationAgainDoesNotDuplicate()
    {
        var before = await h.WithDbAsync(db => db.Categories.CountAsync(c => c.IsDefault));
        Assert.Equal(14, before);

        // Segundo host sobre o MESMO banco: aplica as migrations novamente na inicialização.
        using var second = factory.WithWebHostBuilder(_ => { });
        _ = second.Server;
        await using (var scope = second.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(14, await db.Categories.CountAsync(c => c.IsDefault));
            Assert.Equal(14, await db.Categories.CountAsync(c => c.UserId == null));
        }

        Assert.Equal(14, await h.WithDbAsync(db => db.Categories.CountAsync(c => c.IsDefault)));
    }

    [Fact]
    public async Task Database_RejectsDuplicateNamesPerScope_EvenBypassingTheService()
    {
        var user = await h.NewUserAsync();
        await h.CreateCategoryAsync(user, "Pets");

        // Mesmo nome de uma categoria padrão (escopo user_id NULL) também é rejeitado pelo banco para o escopo padrão.
        var duplicateDefault = await Assert.ThrowsAnyAsync<Exception>(() => h.WithDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO categories (id, user_id, name, type, parent_category_id, icon, color, is_default, is_active) " +
                "VALUES (gen_random_uuid(), NULL, 'ALIMENTAÇÃO', 'Expense', NULL, NULL, NULL, true, true)");
            return 0;
        }));
        Assert.Contains("ux_categories_scope_type_parent_lower_name", duplicateDefault.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_RootAndSubcategory_Returns201_AndTreeFields()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");

        var response = await user.Post("/api/v1/categories", new { name = "  Restaurantes ", type = "Expense", parentCategoryId = food, icon = "pi-star", color = "#AABBCC" });

        var sub = await ApiUser.Read<CategoryDto>(response, HttpStatusCode.Created);
        Assert.Equal("Restaurantes", sub.Name);
        Assert.Equal(food, sub.ParentCategoryId);
        Assert.Equal(CategoryType.Expense, sub.Type);
        Assert.False(sub.IsDefault);
        Assert.True(sub.IsActive);
        Assert.Equal("#AABBCC", sub.Color);

        var all = await user.GetOk<List<CategoryDto>>("/api/v1/categories");
        Assert.Contains(all, c => c.Id == sub.Id);
        Assert.Equal(15, all.Count);
    }

    [Fact]
    public async Task Create_Rules_TypeMismatch_Depth_Duplicates()
    {
        var user = await h.NewUserAsync();
        var food = await h.CategoryIdAsync(user, "Alimentação");
        var sub = await h.CreateCategoryAsync(user, "Restaurantes", parent: food);

        // Tipo diferente do pai → regra de negócio.
        await ReadProblemAsync(
            await user.Post("/api/v1/categories", new { name = "Bônus", type = "Income", parentCategoryId = food }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        // Profundidade máxima 1.
        var depth = await ReadProblemAsync(
            await user.Post("/api/v1/categories", new { name = "Japonês", type = "Expense", parentCategoryId = sub.Id }),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.False(string.IsNullOrEmpty(depth.GetProperty("detail").GetString()));

        // Nome duplicado entre irmãs (sem diferenciar maiúsculas) e igual ao de uma padrão.
        await ReadProblemAsync(
            await user.Post("/api/v1/categories", new { name = "restaurantes", type = "Expense", parentCategoryId = food }),
            HttpStatusCode.Conflict, "conflict");
        await ReadProblemAsync(
            await user.Post("/api/v1/categories", new { name = "SAÚDE", type = "Expense" }),
            HttpStatusCode.Conflict, "conflict");

        // Mesmo nome em outro pai, em outro tipo e para outro usuário é permitido.
        var health = await h.CategoryIdAsync(user, "Saúde");
        await h.CreateCategoryAsync(user, "Restaurantes", parent: health);
        await h.CreateCategoryAsync(user, "Pets", CategoryType.Expense);
        await h.CreateCategoryAsync(user, "Pets", CategoryType.Income);
        var other = await h.NewUserAsync();
        await h.CreateCategoryAsync(other, "Pets");
    }

    [Fact]
    public async Task Create_Validation_ReturnsFieldErrors()
    {
        var user = await h.NewUserAsync();

        var response = await user.Post("/api/v1/categories", new { name = "", type = "Expense", color = "vermelho", icon = "Pi Home" });

        var errors = (await ReadProblemAsync(response, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors");
        Assert.Equal("O nome da categoria é obrigatório.", errors.GetProperty("name")[0].GetString());
        Assert.Equal("A cor deve estar no formato #RRGGBB.", errors.GetProperty("color")[0].GetString());
        Assert.True(errors.TryGetProperty("icon", out _));

        var tooLong = await user.Post("/api/v1/categories", new { name = new string('a', 61), type = "Expense" });
        Assert.True((await ReadProblemAsync(tooLong, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors").TryGetProperty("name", out _));

        var noType = await user.Post("/api/v1/categories", new { name = "Sem tipo" });
        Assert.True((await ReadProblemAsync(noType, HttpStatusCode.BadRequest, "validation_error")).GetProperty("errors").TryGetProperty("type", out _));
    }

    [Fact]
    public async Task Create_WithUnknownOrForeignParent_IsValidationError_NotLeakingExistence()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var privateCat = await h.CreateCategoryAsync(owner, "Privada");

        var foreign = await ReadProblemAsync(
            await intruder.Post("/api/v1/categories", new { name = "Sub", type = "Expense", parentCategoryId = privateCat.Id }),
            HttpStatusCode.BadRequest, "validation_error");
        var unknown = await ReadProblemAsync(
            await intruder.Post("/api/v1/categories", new { name = "Sub", type = "Expense", parentCategoryId = Guid.NewGuid() }),
            HttpStatusCode.BadRequest, "validation_error");
        Assert.Equal(
            foreign.GetProperty("errors").GetProperty("parentCategoryId")[0].GetString(),
            unknown.GetProperty("errors").GetProperty("parentCategoryId")[0].GetString());
    }

    [Fact]
    public async Task DefaultCategories_AreReadOnly_Returns422()
    {
        var user = await h.NewUserAsync();
        var health = await h.CategoryIdAsync(user, "Saúde");

        var update = await ReadProblemAsync(
            await user.Put($"/api/v1/categories/{health}", new { name = "Outro", color = "#000000" }), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Contains("padrão", update.GetProperty("detail").GetString(), StringComparison.Ordinal);
        await ReadProblemAsync(await user.Post($"/api/v1/categories/{health}/deactivate"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        await ReadProblemAsync(await user.Post($"/api/v1/categories/{health}/activate"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var still = (await user.GetOk<List<CategoryDto>>("/api/v1/categories")).Single(c => c.Id == health);
        Assert.Equal("Saúde", still.Name);
        Assert.True(still.IsActive);
    }

    [Fact]
    public async Task Update_ChangesFields_AndRejectsSiblingNameConflict()
    {
        var user = await h.NewUserAsync();
        var pets = await h.CreateCategoryAsync(user, "Pets", color: "#111111");
        await h.CreateCategoryAsync(user, "Viagens");

        var updated = await ApiUser.Read<CategoryDto>(await user.Put($"/api/v1/categories/{pets.Id}", new { name = "Animais", icon = "pi-heart", color = "#222222" }));
        Assert.Equal("Animais", updated.Name);
        Assert.Equal("pi-heart", updated.Icon);
        Assert.Equal("#222222", updated.Color);
        Assert.Equal(CategoryType.Expense, updated.Type);

        await ReadProblemAsync(await user.Put($"/api/v1/categories/{pets.Id}", new { name = "viagens" }), HttpStatusCode.Conflict, "conflict");
        await ReadProblemAsync(await user.Put($"/api/v1/categories/{pets.Id}", new { name = "" }), HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task Deactivate_CascadesToChildren_Activate_RequiresActiveParent()
    {
        var user = await h.NewUserAsync();
        var parent = await h.CreateCategoryAsync(user, "Viagens");
        var child = await h.CreateCategoryAsync(user, "Hotel", parent: parent.Id);

        var off = await ApiUser.Read<CategoryDto>(await user.Post($"/api/v1/categories/{parent.Id}/deactivate"));
        Assert.False(off.IsActive);

        var visible = await user.GetOk<List<CategoryDto>>("/api/v1/categories");
        Assert.DoesNotContain(visible, c => c.Id == parent.Id || c.Id == child.Id);
        var all = await user.GetOk<List<CategoryDto>>("/api/v1/categories?includeInactive=true");
        Assert.False(all.Single(c => c.Id == child.Id).IsActive);

        // Subcategoria só reativa com o pai ativo.
        await ReadProblemAsync(await user.Post($"/api/v1/categories/{child.Id}/activate"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.True((await ApiUser.Read<CategoryDto>(await user.Post($"/api/v1/categories/{parent.Id}/activate"))).IsActive);
        Assert.True((await ApiUser.Read<CategoryDto>(await user.Post($"/api/v1/categories/{child.Id}/activate"))).IsActive);
    }

    [Fact]
    public async Task InactiveCategory_CannotBeUsedInNewTransactions_ButOldOnesRemain()
    {
        var user = await h.NewUserAsync();
        var cat = await h.CreateCategoryAsync(user, "Temporária");
        var existing = await h.CreateTxAsync(user, "Antes", "Expense", 10m, cat.Id, h.Today);
        await user.Post($"/api/v1/categories/{cat.Id}/deactivate");

        await ReadProblemAsync(
            await user.Post("/api/v1/transactions", FinancialHarness.TxBody("Depois", "Expense", 10m, cat.Id, h.Today)),
            HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var got = await user.GetOk<TransactionDetailDto>($"/api/v1/transactions/{existing.Id}");
        Assert.Equal("Temporária", got.CategoryName);

        // Editar outros campos da movimentação antiga (sem trocar de categoria) continua permitido.
        var edited = await user.Put($"/api/v1/transactions/{existing.Id}", new { description = "Antes (editada)", amount = 10m, categoryId = cat.Id, transactionDate = h.Today });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
    }

    [Fact]
    public async Task OtherUsersCategories_AreInvisible_AndNotFoundOnWrite()
    {
        var owner = await h.NewUserAsync();
        var intruder = await h.NewUserAsync();
        var cat = await h.CreateCategoryAsync(owner, "Privada");

        Assert.DoesNotContain(await intruder.GetOk<List<CategoryDto>>("/api/v1/categories?includeInactive=true"), c => c.Id == cat.Id);
        await ReadProblemAsync(await intruder.Put($"/api/v1/categories/{cat.Id}", new { name = "x" }), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Post($"/api/v1/categories/{cat.Id}/deactivate"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Post($"/api/v1/categories/{cat.Id}/activate"), HttpStatusCode.NotFound, "not_found");
        await ReadProblemAsync(await intruder.Put($"/api/v1/categories/{Guid.NewGuid()}", new { name = "x" }), HttpStatusCode.NotFound, "not_found");

        // Categoria alheia não pode ser usada em movimentações (resposta idêntica à de categoria inexistente).
        var foreign = await ReadProblemAsync(
            await intruder.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, cat.Id, h.Today)), HttpStatusCode.BadRequest, "validation_error");
        var unknown = await ReadProblemAsync(
            await intruder.Post("/api/v1/transactions", FinancialHarness.TxBody("x", "Expense", 1m, Guid.NewGuid(), h.Today)), HttpStatusCode.BadRequest, "validation_error");
        Assert.Equal(
            foreign.GetProperty("errors").GetProperty("categoryId")[0].GetString(),
            unknown.GetProperty("errors").GetProperty("categoryId")[0].GetString());
    }

    [Fact]
    public async Task InvalidTypeFilter_IsValidationError()
    {
        var user = await h.NewUserAsync();
        await ReadProblemAsync(await user.Get("/api/v1/categories?type=Banana"), HttpStatusCode.BadRequest, "validation_error");
    }
}
