using System.Text.RegularExpressions;
using FinanceApp.Domain.Users;
using FluentValidation;

namespace FinanceApp.Application.Common.Validation;

/// <summary>Regras reutilizáveis (mensagens em pt-BR conforme docs/API_CONTRACTS.md).</summary>
public static partial class CommonRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    /// <summary>Política de senha (D-007): 8–128 caracteres, ao menos uma letra e um número.</summary>
    public static IRuleBuilderOptions<T, string> PasswordPolicy<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("A senha é obrigatória.")
            .MinimumLength(PasswordMinLength).WithMessage($"A senha deve ter ao menos {PasswordMinLength} caracteres.")
            .MaximumLength(PasswordMaxLength).WithMessage($"A senha deve ter no máximo {PasswordMaxLength} caracteres.")
            .Must(p => p.Any(char.IsLetter)).WithMessage("A senha deve conter ao menos uma letra.")
            .Must(p => p.Any(char.IsDigit)).WithMessage("A senha deve conter ao menos um número.");

    /// <summary>E-mail obrigatório, formato válido e até 254 caracteres (avaliado após trim).</summary>
    public static IRuleBuilderOptions<T, string> EmailAddressRules<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O e-mail é obrigatório.")
            .Must(e => e.Trim().Length <= User.EmailMaxLength)
            .WithMessage($"O e-mail deve ter no máximo {User.EmailMaxLength} caracteres.")
            .Must(e => IsValidEmail(e.Trim())).WithMessage("E-mail inválido.");

    /// <summary>Nome obrigatório com 2–100 caracteres (avaliado após trim).</summary>
    public static IRuleBuilderOptions<T, string> PersonNameRules<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O nome é obrigatório.")
            .Must(n => n.Trim().Length is >= User.NameMinLength and <= User.NameMaxLength)
            .WithMessage($"O nome deve ter entre {User.NameMinLength} e {User.NameMaxLength} caracteres.");

    public static bool IsValidEmail(string email) => EmailRegex().IsMatch(email);

    // Pragmático: parte local e domínio sem espaços/@, domínio com ao menos um ponto e TLD ≥ 2.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s.]{2,}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailRegex();
}
