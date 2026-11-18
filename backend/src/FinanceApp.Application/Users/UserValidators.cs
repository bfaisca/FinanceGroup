using FinanceApp.Application.Common.Validation;
using FluentValidation;

namespace FinanceApp.Application.Users;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name).PersonNameRules();
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("A senha atual é obrigatória.")
            .MaximumLength(CommonRules.PasswordMaxLength).WithMessage("Senha atual inválida.");

        RuleFor(x => x.NewPassword).PasswordPolicy();

        RuleFor(x => x.NewPassword)
            .NotEqual(x => x.CurrentPassword, StringComparer.Ordinal)
            .When(x => !string.IsNullOrEmpty(x.NewPassword))
            .WithMessage("A nova senha deve ser diferente da atual.");
    }
}

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MaximumLength(CommonRules.PasswordMaxLength).WithMessage("Senha inválida.");
    }
}
