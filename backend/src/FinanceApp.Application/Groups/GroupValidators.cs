using FinanceApp.Application.Common.Validation;
using FinanceApp.Domain.Groups;
using FluentValidation;

namespace FinanceApp.Application.Groups;

internal static class GroupRules
{
    public static IRuleBuilderOptions<T, string> GroupName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O nome do grupo é obrigatório.")
            .Must(n => n.Trim().Length is >= 1 and <= Group.NameMaxLength)
            .WithMessage($"O nome do grupo deve ter entre 1 e {Group.NameMaxLength} caracteres.");

    public static IRuleBuilderOptions<T, string?> GroupDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(d => string.IsNullOrWhiteSpace(d) || d.Trim().Length <= Group.DescriptionMaxLength)
            .WithMessage($"A descrição deve ter no máximo {Group.DescriptionMaxLength} caracteres.");
}

public sealed class CreateGroupRequestValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupRequestValidator()
    {
        RuleFor(x => x.Name).GroupName();
        RuleFor(x => x.Description).GroupDescription();
    }
}

public sealed class UpdateGroupRequestValidator : AbstractValidator<UpdateGroupRequest>
{
    public UpdateGroupRequestValidator()
    {
        RuleFor(x => x.Name).GroupName();
        RuleFor(x => x.Description).GroupDescription();
    }
}

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator() =>
        RuleFor(x => x.Role)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("O papel é obrigatório.")
            .Must(r => r is GroupMemberRole.Admin or GroupMemberRole.Member)
            .WithMessage("O papel deve ser Admin ou Member.");
}

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator() => RuleFor(x => x.Email).EmailAddressRules();
}
