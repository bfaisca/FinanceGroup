using FinanceApp.Application.Categories;
using FinanceApp.Domain.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>Categorias padrão (somente leitura) e personalizadas (contrato §7).</summary>
[ApiController]
[Authorize]
[Route("api/v1/categories")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    /// <summary>Lista plana (padrão + do usuário); o frontend monta a árvore.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(
        [FromQuery] CategoryType? type,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken) =>
        Ok(await categories.ListAsync(type, includeInactive, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Create([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categories.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/categories/{category.Id}", category);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await categories.UpdateAsync(id, request, cancellationToken));

    /// <summary>Desativa a categoria e suas subcategorias. Categorias padrão: 422.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        Ok(await categories.DeactivateAsync(id, cancellationToken));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await categories.ActivateAsync(id, cancellationToken));
}
