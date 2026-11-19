using FinanceApp.Application.Categories;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Categories;
using FinanceApp.Domain.Common;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.UnitTests.Support;
using Microsoft.EntityFrameworkCore;
using static FinanceApp.UnitTests.Support.Fixtures;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.UnitTests.Financial;

public sealed class CategoryTests : IDisposable
{
    private readonly AppDbContext db = TestDb.Create();
    private readonly FakeCurrentUser currentUser = new() { UserId = UserId };
    private readonly CategoryService service;

    public CategoryTests()
    {
        service = new CategoryService(db, currentUser, new CreateCategoryRequestValidator(), new UpdateCategoryRequestValidator());
    }

    public void Dispose() => db.Dispose();

    private static CreateCategoryRequest Create(string name, CategoryType type = CategoryType.Expense, Guid? parent = null, string? icon = null, string? color = null) =>
        new() { Name = name, Type = type, ParentCategoryId = parent, Icon = icon, Color = color };

    private Guid DefaultId(string name, CategoryType type) =>
        DefaultCategories.All.Single(c => c.Name == name && c.Type == type).Id;

    // ------------------------------------------------------------ seed

    [Fact]
    public void DefaultCategories_HaveFixedUniqueIds_AndExpectedNames()
    {
        Assert.Equal(14, DefaultCategories.All.Count);
        Assert.Equal(14, DefaultCategories.All.Select(c => c.Id).Distinct().Count());
        Assert.Equal(
            ["Alimentação", "Saúde", "Educação", "Lazer", "Assinaturas", "Moradia", "Transporte", "Compras", "Outros"],
            DefaultCategories.All.Where(c => c.Type == CategoryType.Expense).Select(c => c.Name));
        Assert.Equal(
            ["Salário", "Freelance", "Investimentos", "Reembolsos", "Outros"],
            DefaultCategories.All.Where(c => c.Type == CategoryType.Income).Select(c => c.Name));
        Assert.All(DefaultCategories.All, c =>
        {
            Assert.True(Category.IsValidIcon(c.Icon));
            Assert.StartsWith("pi-", c.Icon, StringComparison.Ordinal);
            Assert.True(Category.IsValidColor(c.Color));
        });
        // IDs estáveis (não podem mudar entre ambientes/releases).
        Assert.Equal(Guid.Parse("00000000-0000-0000-0001-000000000001"), DefaultCategories.All[0].Id);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0002-000000000001"), DefaultCategories.All[9].Id);
    }

    [Fact]
    public async Task List_ReturnsSeededDefaults_FilteredByType()
    {
        var all = await service.ListAsync(null, false, default);
        Assert.Equal(14, all.Count);
        Assert.All(all, c => Assert.True(c.IsDefault));

        var expenses = await service.ListAsync(CategoryType.Expense, false, default);
        Assert.Equal(9, expenses.Count);
        var incomes = await service.ListAsync(CategoryType.Income, false, default);
        Assert.Equal(5, incomes.Count);
    }

    // ------------------------------------------------------------ criação e hierarquia

    [Fact]
    public async Task Create_RootCategory()
    {
        var created = await service.CreateAsync(Create("Pets", color: "#FF8800", icon: "pi-star"), default);

        Assert.False(created.IsDefault);
        Assert.True(created.IsActive);
        Assert.Null(created.ParentCategoryId);
        Assert.Equal("#FF8800", created.Color);
        Assert.Equal("pi-star", created.Icon);
        Assert.Equal(15, (await service.ListAsync(null, false, default)).Count);
    }

    [Fact]
    public async Task Create_Subcategory_UnderDefault_InheritsType()
    {
        var parent = DefaultId("Alimentação", CategoryType.Expense);

        var sub = await service.CreateAsync(Create("Restaurantes", parent: parent), default);

        Assert.Equal(parent, sub.ParentCategoryId);
        Assert.Equal(CategoryType.Expense, sub.Type);
    }

    [Fact]
    public async Task Create_Subcategory_WithDifferentTypeThanParent_Fails()
    {
        var parent = DefaultId("Alimentação", CategoryType.Expense);
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(Create("Bônus", CategoryType.Income, parent), default));
    }

    [Fact]
    public async Task Create_Subcategory_OfSubcategory_ExceedsMaxDepth()
    {
        var parent = DefaultId("Alimentação", CategoryType.Expense);
        var sub = await service.CreateAsync(Create("Restaurantes", parent: parent), default);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(Create("Japonês", parent: sub.Id), default));
        Assert.Contains("profundidade", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_Subcategory_UnderInactiveParent_Fails()
    {
        var parent = await service.CreateAsync(Create("Viagens"), default);
        await service.DeactivateAsync(parent.Id, default);
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(Create("Hotel", parent: parent.Id), default));
    }

    [Fact]
    public async Task Create_Subcategory_UnderUnknownOrForeignParent_IsValidationError()
    {
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Create("X", parent: Guid.NewGuid()), default));

        currentUser.UserId = OtherUserId;
        var foreign = await service.CreateAsync(Create("Privada"), default);
        currentUser.UserId = UserId;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Create("Sub", parent: foreign.Id), default));
        Assert.True(ex.Errors.ContainsKey("parentCategoryId"));
    }

    // ------------------------------------------------------------ duplicidade

    [Fact]
    public async Task Create_DuplicateAmongSiblings_IgnoresCase_Conflict()
    {
        await service.CreateAsync(Create("Pets"), default);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Create("  pETS "), default));
        Assert.Equal(ErrorCodes.Conflict, ex.Code);
    }

    [Fact]
    public async Task Create_RootWithSameNameAsDefaultOfSameType_Conflict() =>
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Create("alimentação"), default));

    [Fact]
    public async Task Create_SameNameInDifferentType_IsAllowed()
    {
        // "Outros" existe como padrão de despesa e de receita; "Pets" pode existir nos dois tipos.
        await service.CreateAsync(Create("Pets"), default);
        var income = await service.CreateAsync(Create("Pets", CategoryType.Income), default);
        Assert.Equal(CategoryType.Income, income.Type);
    }

    [Fact]
    public async Task Create_SameNameUnderDifferentParents_IsAllowed()
    {
        var a = DefaultId("Alimentação", CategoryType.Expense);
        var b = DefaultId("Lazer", CategoryType.Expense);
        await service.CreateAsync(Create("Outros", parent: a), default);
        await service.CreateAsync(Create("Outros", parent: b), default);
    }

    [Fact]
    public async Task Create_SameNameUnderSameParent_Conflict()
    {
        var a = DefaultId("Alimentação", CategoryType.Expense);
        await service.CreateAsync(Create("Padaria", parent: a), default);
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Create("PADARIA", parent: a), default));
    }

    [Fact]
    public async Task Create_SameNameAsAnotherUsersCategory_IsAllowed()
    {
        currentUser.UserId = OtherUserId;
        await service.CreateAsync(Create("Pets"), default);
        currentUser.UserId = UserId;
        await service.CreateAsync(Create("Pets"), default);
    }

    // ------------------------------------------------------------ padrão imutável

    [Fact]
    public async Task Default_CannotBeUpdated()
    {
        var id = DefaultId("Saúde", CategoryType.Expense);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            service.UpdateAsync(id, new UpdateCategoryRequest { Name = "Outro nome" }, default));
        Assert.Contains("padrão", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Default_CannotBeDeactivated_NorActivated()
    {
        var id = DefaultId("Saúde", CategoryType.Expense);
        await Assert.ThrowsAsync<DomainException>(() => service.DeactivateAsync(id, default));
        await Assert.ThrowsAsync<DomainException>(() => service.ActivateAsync(id, default));
        Assert.True(db.Categories.Single(c => c.Id == id).IsActive);
    }

    // ------------------------------------------------------------ atualização e ativação

    [Fact]
    public async Task Update_ChangesNameIconColor()
    {
        var created = await service.CreateAsync(Create("Pets"), default);

        var updated = await service.UpdateAsync(created.Id, new UpdateCategoryRequest { Name = "Animais", Icon = "pi-heart", Color = "#00FF00" }, default);

        Assert.Equal("Animais", updated.Name);
        Assert.Equal("pi-heart", updated.Icon);
        Assert.Equal("#00FF00", updated.Color);
    }

    [Fact]
    public async Task Update_ToNameOfSibling_Conflict()
    {
        await service.CreateAsync(Create("Pets"), default);
        var other = await service.CreateAsync(Create("Viagens"), default);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateAsync(other.Id, new UpdateCategoryRequest { Name = "pets" }, default));
    }

    [Fact]
    public async Task Update_KeepingOwnName_IsAllowed()
    {
        var created = await service.CreateAsync(Create("Pets"), default);
        var updated = await service.UpdateAsync(created.Id, new UpdateCategoryRequest { Name = "PETS", Color = "#112233" }, default);
        Assert.Equal("PETS", updated.Name);
    }

    [Fact]
    public async Task Update_AnotherUsersCategory_IsNotFound()
    {
        currentUser.UserId = OtherUserId;
        var foreign = await service.CreateAsync(Create("Privada"), default);
        currentUser.UserId = UserId;

        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(foreign.Id, new UpdateCategoryRequest { Name = "x" }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeactivateAsync(foreign.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ActivateAsync(foreign.Id, default));
    }

    [Fact]
    public async Task List_DoesNotShowOtherUsersCategories()
    {
        currentUser.UserId = OtherUserId;
        await service.CreateAsync(Create("Privada"), default);
        currentUser.UserId = UserId;

        Assert.DoesNotContain(await service.ListAsync(null, true, default), c => c.Name == "Privada");
    }

    [Fact]
    public async Task Deactivate_CascadesToSubcategories_AndListHidesThemUnlessRequested()
    {
        var parent = await service.CreateAsync(Create("Viagens"), default);
        var child = await service.CreateAsync(Create("Hotel", parent: parent.Id), default);

        var result = await service.DeactivateAsync(parent.Id, default);

        Assert.False(result.IsActive);
        var active = await service.ListAsync(null, false, default);
        Assert.DoesNotContain(active, c => c.Id == parent.Id || c.Id == child.Id);
        var all = await service.ListAsync(null, true, default);
        Assert.False(all.Single(c => c.Id == child.Id).IsActive);
    }

    [Fact]
    public async Task Activate_Subcategory_RequiresActiveParent()
    {
        var parent = await service.CreateAsync(Create("Viagens"), default);
        var child = await service.CreateAsync(Create("Hotel", parent: parent.Id), default);
        await service.DeactivateAsync(parent.Id, default);

        await Assert.ThrowsAsync<DomainException>(() => service.ActivateAsync(child.Id, default));

        await service.ActivateAsync(parent.Id, default);
        var reactivated = await service.ActivateAsync(child.Id, default);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task Activate_Parent_DoesNotReactivateChildren()
    {
        var parent = await service.CreateAsync(Create("Viagens"), default);
        var child = await service.CreateAsync(Create("Hotel", parent: parent.Id), default);
        await service.DeactivateAsync(parent.Id, default);

        await service.ActivateAsync(parent.Id, default);

        Assert.False((await service.ListAsync(null, true, default)).Single(c => c.Id == child.Id).IsActive);
    }

    // ------------------------------------------------------------ validação

    [Theory]
    [InlineData("", "O nome da categoria é obrigatório.")]
    [InlineData("   ", "O nome da categoria é obrigatório.")]
    public async Task Create_RequiresName(string name, string message)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Create(name), default));
        Assert.Equal(message, ex.Errors["name"][0]);
    }

    [Fact]
    public async Task Create_NameLongerThan60_IsInvalid()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Create(new string('a', 61)), default));
        Assert.True(ex.Errors.ContainsKey("name"));
    }

    [Fact]
    public async Task Create_NameWith60Chars_IsValid() =>
        Assert.Equal(60, (await service.CreateAsync(Create(new string('a', 60)), default)).Name.Length);

    [Theory]
    [InlineData("red")]
    [InlineData("#FFF")]
    [InlineData("#GGGGGG")]
    [InlineData("123456")]
    [InlineData("#1234567")]
    public async Task Create_InvalidColor_IsRejected(string color)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Create("Pets", color: color), default));
        Assert.Equal("A cor deve estar no formato #RRGGBB.", ex.Errors["color"][0]);
    }

    [Theory]
    [InlineData("#a1B2c3")]
    [InlineData("#000000")]
    public async Task Create_ValidColor_IsAccepted(string color) =>
        Assert.Equal(color, (await service.CreateAsync(Create("Pets", color: color), default)).Color);

    [Theory]
    [InlineData("pi pi-home")]
    [InlineData("<script>")]
    [InlineData("Home")]
    public async Task Create_InvalidIcon_IsRejected(string icon)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Create("Pets", icon: icon), default));
        Assert.True(ex.Errors.ContainsKey("icon"));
    }

    [Fact]
    public async Task Create_MissingType_IsValidationError()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new CreateCategoryRequest { Name = "Pets" }, default));
        Assert.Equal("O tipo da categoria é obrigatório.", ex.Errors["type"][0]);
    }

    [Fact]
    public async Task Create_BlankIconAndColor_BecomeNull()
    {
        var created = await service.CreateAsync(Create("Pets", icon: "  ", color: ""), default);
        Assert.Null(created.Icon);
        Assert.Null(created.Color);
    }
}
