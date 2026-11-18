using FinanceApp.Domain.Common;
using FinanceApp.Domain.Users;

namespace FinanceApp.UnitTests.Domain;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesEmailAndName()
    {
        var user = User.Create("  Maria Silva ", "  Maria@Exemplo.COM ", "hash", Now);

        Assert.Equal("Maria Silva", user.Name);
        Assert.Equal("maria@exemplo.com", user.Email);
        Assert.True(user.IsActive);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(Now, user.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" a ")]
    public void Create_InvalidName_Throws(string name)
    {
        Assert.Throws<DomainException>(() => User.Create(name, "a@b.com", "hash", Now));
    }

    [Fact]
    public void Rename_UpdatesNameAndTimestamp()
    {
        var user = User.Create("Maria", "a@b.com", "hash", Now);
        user.Rename(" Maria S. Silva ", Now.AddMinutes(5));

        Assert.Equal("Maria S. Silva", user.Name);
        Assert.Equal(Now.AddMinutes(5), user.UpdatedAt);
    }

    [Fact]
    public void Anonymize_RemovesPersonalDataAndDeactivates()
    {
        var user = User.Create("Maria Silva", "maria@exemplo.com", "hash", Now);
        var id = user.Id;

        user.Anonymize(Now.AddDays(1));

        Assert.Equal(id, user.Id);
        Assert.Equal(User.AnonymizedName, user.Name);
        Assert.DoesNotContain("maria", user.Email, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("@" + User.AnonymizedEmailDomain, user.Email, StringComparison.Ordinal);
        Assert.Equal(User.DisabledPasswordHash, user.PasswordHash);
        Assert.False(user.IsActive);
        Assert.Equal(Now.AddDays(1), user.UpdatedAt);
    }

    [Fact]
    public void Anonymize_GeneratesUniqueEmails()
    {
        var a = User.Create("Ana", "a@b.com", "h", Now);
        var b = User.Create("Bia", "b@b.com", "h", Now);
        a.Anonymize(Now);
        b.Anonymize(Now);
        Assert.NotEqual(a.Email, b.Email);
    }

    [Fact]
    public void InactiveUser_CannotBeChanged()
    {
        var user = User.Create("Maria", "a@b.com", "hash", Now);
        user.Anonymize(Now);

        Assert.Throws<DomainException>(() => user.Rename("Outro", Now));
        Assert.Throws<DomainException>(() => user.ChangePasswordHash("x", Now));
        Assert.Throws<DomainException>(() => user.Anonymize(Now));
    }
}
