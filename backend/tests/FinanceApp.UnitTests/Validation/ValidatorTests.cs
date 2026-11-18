using FinanceApp.Application.Auth;
using FinanceApp.Application.Common.Validation;
using FinanceApp.Application.Users;

namespace FinanceApp.UnitTests.Validation;

public class ValidatorTests
{
    private readonly RegisterRequestValidator register = new();

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        var result = register.Validate(new RegisterRequest { Name = "Maria Silva", Email = "maria@exemplo.com", Password = "Senha123" });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "A senha é obrigatória.")]
    [InlineData("Abc123", "A senha deve ter ao menos 8 caracteres.")]
    [InlineData("abcdefgh", "A senha deve conter ao menos um número.")]
    [InlineData("12345678", "A senha deve conter ao menos uma letra.")]
    public void Register_PasswordPolicy(string password, string expectedMessage)
    {
        var result = register.Validate(new RegisterRequest { Name = "Maria", Email = "maria@exemplo.com", Password = password });

        var error = Assert.Single(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Password));
        Assert.Equal(expectedMessage, error.ErrorMessage);
    }

    [Fact]
    public void Register_PasswordTooLong_Fails()
    {
        var result = register.Validate(new RegisterRequest { Name = "Maria", Email = "maria@exemplo.com", Password = new string('a', 128) + "1" });
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A senha deve ter no máximo 128 caracteres.");
    }

    [Theory]
    [InlineData("maria")]
    [InlineData("maria@")]
    [InlineData("maria@exemplo")]
    [InlineData("ma ria@exemplo.com")]
    [InlineData("maria@@exemplo.com")]
    public void Register_InvalidEmail_Fails(string email)
    {
        var result = register.Validate(new RegisterRequest { Name = "Maria", Email = email, Password = "Senha123" });
        var error = Assert.Single(result.Errors);
        Assert.Equal("E-mail inválido.", error.ErrorMessage);
    }

    [Theory]
    [InlineData("  Maria@Exemplo.com  ")]
    [InlineData("maria.silva+financas@sub.exemplo.com.br")]
    public void Register_ValidEmailVariants_Pass(string email)
    {
        var result = register.Validate(new RegisterRequest { Name = "Maria", Email = email, Password = "Senha123" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Register_EmailTooLong_Fails()
    {
        var email = new string('a', 250) + "@x.com";
        var result = register.Validate(new RegisterRequest { Name = "Maria", Email = email, Password = "Senha123" });
        Assert.Contains(result.Errors, e => e.ErrorMessage == "O e-mail deve ter no máximo 254 caracteres.");
    }

    [Theory]
    [InlineData("", "O nome é obrigatório.")]
    [InlineData("   ", "O nome é obrigatório.")]
    [InlineData(" A ", "O nome deve ter entre 2 e 100 caracteres.")]
    public void Register_InvalidName_Fails(string name, string message)
    {
        var result = register.Validate(new RegisterRequest { Name = name, Email = "a@b.com", Password = "Senha123" });
        Assert.Equal(message, Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void Register_NullFields_FailWithoutExceptions()
    {
        var result = register.Validate(new RegisterRequest { Name = null!, Email = null!, Password = null! });
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void ChangePassword_NewMustDifferFromCurrent()
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest { CurrentPassword = "Senha123", NewPassword = "Senha123" });
        Assert.Equal("A nova senha deve ser diferente da atual.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void ResetPassword_RequiresTokenAndPolicy()
    {
        var validator = new ResetPasswordRequestValidator();
        var result = validator.Validate(new ResetPasswordRequest { Token = "", NewPassword = "fraca" });
        Assert.Contains(result.Errors, e => e.PropertyName == "Token");
        Assert.Contains(result.Errors, e => e.PropertyName == "NewPassword");
    }

    [Theory]
    [InlineData("Email", "email")]
    [InlineData("NewPassword", "newPassword")]
    [InlineData("Items[0].UnitPrice", "items[0].unitPrice")]
    public void ToCamelCasePath_ConvertsSegments(string input, string expected)
    {
        Assert.Equal(expected, ValidationExtensions.ToCamelCasePath(input));
    }

    [Fact]
    public async Task EnsureValidAsync_ThrowsWithCamelCaseKeys()
    {
        var ex = await Assert.ThrowsAsync<FinanceApp.Application.Common.Exceptions.ValidationException>(() =>
            register.EnsureValidAsync(new RegisterRequest { Name = "Maria", Email = "x", Password = "Senha123" }, CancellationToken.None));

        Assert.Equal("validation_error", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(["E-mail inválido."], ex.Errors["email"]);
    }
}
