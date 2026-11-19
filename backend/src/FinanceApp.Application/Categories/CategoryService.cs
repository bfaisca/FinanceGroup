using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Categories;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ValidationException = FinanceApp.Application.Common.Exceptions.ValidationException;

namespace FinanceApp.Application.Categories;

/// <summary>Categorias padrão (somente leitura) e personalizadas do usuário autenticado.</summary>
public sealed class CategoryService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator)
{
    /// <summary>Lista plana: padrão + personalizadas do usuário. O frontend monta a árvore.</summary>
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CategoryType? type, bool includeInactive, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = db.Categories.AsNoTracking().Where(c => c.UserId == null || c.UserId == userId);
        if (type is { } t)
        {
            query = query.Where(c => c.Type == t);
        }

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .OrderBy(c => c.Type)
            .ThenByDescending(c => c.IsDefault)
            .ThenBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return categories.Select(CategoryDto.From).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();

        Category? parent = null;
        if (request.ParentCategoryId is { } parentId)
        {
            parent = await FindVisibleAsync(parentId, tracking: false, cancellationToken).ConfigureAwait(false)
                ?? throw new ValidationException("parentCategoryId", "Categoria pai não encontrada.");
        }

        var category = Category.CreateCustom(userId, request.Name, request.Type!.Value, parent, request.Icon, request.Color);
        await EnsureNameAvailableAsync(userId, category.Name, category.Type, category.ParentCategoryId, null, cancellationToken).ConfigureAwait(false);

        db.Categories.Add(category);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return CategoryDto.From(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        var userId = currentUser.RequireUserId();
        var category = await GetVisibleAsync(id, cancellationToken).ConfigureAwait(false);

        category.Update(request.Name, request.Icon, request.Color);
        await EnsureNameAvailableAsync(userId, category.Name, category.Type, category.ParentCategoryId, category.Id, cancellationToken).ConfigureAwait(false);
        await SaveAsync(cancellationToken).ConfigureAwait(false);
        return CategoryDto.From(category);
    }

    /// <summary>Desativa a categoria e todas as suas subcategorias.</summary>
    public async Task<CategoryDto> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await GetVisibleAsync(id, cancellationToken).ConfigureAwait(false);
        category.Deactivate();

        var children = await db.Categories
            .Where(c => c.ParentCategoryId == category.Id && c.IsActive)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var child in children)
        {
            child.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CategoryDto.From(category);
    }

    public async Task<CategoryDto> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await GetVisibleAsync(id, cancellationToken).ConfigureAwait(false);
        Category? parent = null;
        if (category.ParentCategoryId is { } parentId)
        {
            parent = await db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken).ConfigureAwait(false);
        }

        category.Activate(parent);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return CategoryDto.From(category);
    }

    /// <summary>Categoria padrão ou do usuário; qualquer outra (inclusive de terceiros) é 404.</summary>
    private async Task<Category> GetVisibleAsync(Guid id, CancellationToken cancellationToken) =>
        await FindVisibleAsync(id, tracking: true, cancellationToken).ConfigureAwait(false)
        ?? throw new NotFoundException("Categoria não encontrada.");

    private async Task<Category?> FindVisibleAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var query = tracking ? db.Categories.AsTracking() : db.Categories.AsNoTracking();
        return await query.FirstOrDefaultAsync(c => c.Id == id && (c.UserId == null || c.UserId == userId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Nome único entre irmãs (mesmo pai e tipo) visíveis ao usuário, inclusive as padrão.</summary>
    private async Task EnsureNameAvailableAsync(
        Guid userId,
        string name,
        CategoryType type,
        Guid? parentId,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
#pragma warning disable CA1862, CA1304, CA1311 // Traduzido para SQL lower(): o provedor só traduz ToLower() e não StringComparison.
        var lower = name.ToLowerInvariant();
        var exists = await db.Categories.AnyAsync(
                c => (c.UserId == null || c.UserId == userId)
                     && c.Type == type
                     && c.ParentCategoryId == parentId
                     && c.Id != exceptId
                     && c.Name.ToLower() == lower,
                cancellationToken)
            .ConfigureAwait(false);
#pragma warning restore CA1311, CA1304, CA1862
        if (exists)
        {
            throw DuplicateName();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (UniqueConstraintViolationException)
        {
            throw DuplicateName();
        }
    }

    private static ConflictException DuplicateName() =>
        new(ErrorCodes.Conflict, "Conflito com um registro existente.", "Já existe uma categoria com este nome neste nível.");
}
