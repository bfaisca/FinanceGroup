using FinanceApp.Application.Groups;
using FinanceApp.Domain.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceApp.API.Controllers;

/// <summary>
/// Grupos, participantes, convites e dashboard do grupo (contrato §16). Quem não é participante ativo recebe 404;
/// papel insuficiente, 403. Nenhuma resposta contém conta, cartão, saldo ou e-mail de participante.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/groups")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class GroupsController(
    GroupService groups,
    GroupInvitationService invitations,
    GroupDashboardService dashboard) : ControllerBase
{
    /// <summary>Meus grupos (participante ativo), em ordem alfabética. Arquivados somente com <c>includeArchived=true</c>.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GroupSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GroupSummaryDto>>> List([FromQuery] bool includeArchived = false, CancellationToken cancellationToken = default) =>
        Ok(await groups.ListAsync(includeArchived, cancellationToken));

    /// <summary>Cria o grupo; o criador torna-se Owner.</summary>
    [HttpPost]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<GroupDetailDto>> Create([FromBody] CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var created = await groups.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<GroupDetailDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await groups.GetAsync(id, cancellationToken));

    /// <summary>Edita nome/descrição (Admin/Owner; grupo ativo).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupDetailDto>> Update(Guid id, [FromBody] UpdateGroupRequest request, CancellationToken cancellationToken) =>
        Ok(await groups.UpdateAsync(id, request, cancellationToken));

    /// <summary>Arquiva (somente leitura; nada é excluído). Admin/Owner.</summary>
    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupDetailDto>> Archive(Guid id, CancellationToken cancellationToken) =>
        Ok(await groups.ArchiveAsync(id, cancellationToken));

    [HttpPost("{id:guid}/unarchive")]
    [ProducesResponseType<GroupDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupDetailDto>> Unarchive(Guid id, CancellationToken cancellationToken) =>
        Ok(await groups.UnarchiveAsync(id, cancellationToken));

    /// <summary>Sai do grupo (o Owner não pode: 422). O histórico é preservado.</summary>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        await groups.LeaveAsync(id, cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------ participantes

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType<IReadOnlyList<GroupMemberDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<GroupMemberDto>>> ListMembers(Guid id, [FromQuery] bool includeFormer = false, CancellationToken cancellationToken = default) =>
        Ok(await groups.ListMembersAsync(id, includeFormer, cancellationToken));

    /// <summary>Promove Member→Admin ou rebaixa Admin→Member (somente o Owner rebaixa). O Owner não pode ser alterado.</summary>
    [HttpPut("{id:guid}/members/{memberId:guid}/role")]
    [ProducesResponseType<GroupMemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupMemberDto>> ChangeRole(Guid id, Guid memberId, [FromBody] ChangeMemberRoleRequest request, CancellationToken cancellationToken) =>
        Ok(await groups.ChangeRoleAsync(id, memberId, request, cancellationToken));

    /// <summary>Remove um participante (status <c>Removed</c>, histórico preservado). Owner remove Admin/Member; Admin remove Member.</summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    {
        await groups.RemoveMemberAsync(id, memberId, cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------ convites

    /// <summary>Convites do grupo (Admin/Owner), do mais recente ao mais antigo. <c>Expired</c> é derivado.</summary>
    [HttpGet("{id:guid}/invitations")]
    [ProducesResponseType<IReadOnlyList<GroupInvitationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<GroupInvitationDto>>> ListInvitations(Guid id, [FromQuery] GroupInvitationStatus? status, CancellationToken cancellationToken) =>
        Ok(await invitations.ListAsync(id, status, cancellationToken));

    /// <summary>Envia convite por e-mail (token nunca é devolvido). <c>emailSent=false</c> se o envio falhar; o convite existe e pode ser reenviado.</summary>
    [HttpPost("{id:guid}/invitations")]
    [ProducesResponseType<GroupInvitationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupInvitationDto>> CreateInvitation(Guid id, [FromBody] CreateInvitationRequest request, CancellationToken cancellationToken)
    {
        var created = await invitations.CreateAsync(id, request, cancellationToken);
        return Created($"/api/v1/groups/{id}/invitations", created);
    }

    /// <summary>Reenvia: novo token e nova validade (o token anterior deixa de valer).</summary>
    [HttpPost("{id:guid}/invitations/{invitationId:guid}/resend")]
    [ProducesResponseType<GroupInvitationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<GroupInvitationDto>> ResendInvitation(Guid id, Guid invitationId, CancellationToken cancellationToken) =>
        Ok(await invitations.ResendAsync(id, invitationId, cancellationToken));

    /// <summary>Revoga um convite pendente.</summary>
    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> RevokeInvitation(Guid id, Guid invitationId, CancellationToken cancellationToken)
    {
        await invitations.RevokeAsync(id, invitationId, cancellationToken);
        return NoContent();
    }

    // ------------------------------------------------------------------ dashboard

    /// <summary>
    /// Dashboard do grupo (D-034): totais, participação por quem CADASTROU e por quem PAGOU (separados), categorias, evolução
    /// mensal e comparação com o período anterior. Percentuais são indicadores estatísticos, não dívidas.
    /// </summary>
    [HttpGet("{id:guid}/dashboard")]
    [ProducesResponseType<GroupDashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<GroupDashboardDto>> Dashboard(Guid id, [FromQuery] GroupDashboardQuery query, CancellationToken cancellationToken) =>
        Ok(await dashboard.GetAsync(id, query, cancellationToken));
}
