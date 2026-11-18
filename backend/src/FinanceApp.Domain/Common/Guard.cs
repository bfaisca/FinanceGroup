namespace FinanceApp.Domain.Common;

internal static class Guard
{
    public static string NotBlank(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(message);
        }

        return value;
    }

    public static Guid NotEmpty(Guid value, string message)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException(message);
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value) => value.ToUniversalTime();
}
