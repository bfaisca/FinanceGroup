using FinanceApp.Application.Common.Validation;
using FluentValidation;

namespace FinanceApp.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Name).PersonNameRules();
        RuleFor(x => x.Email).EmailAddressRules();
        RuleFor(x => x.Password).PasswordPolicy();
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).EmailAddressRules();
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MaximumLength(CommonRules.PasswordMaxLength).WithMessage("Senha inválida.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).EmailAddressRules();
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public const int TokenMaxLength = 512;

    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("O token é obrigatório.")
            .MaximumLength(TokenMaxLength).WithMessage("Token inválido.");
        RuleFor(x => x.NewPassword).PasswordPolicy();
    }
}
