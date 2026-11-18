using FinanceApp.Domain.Users;

namespace FinanceApp.Application.Users;

public sealed record UserDto(Guid Id, string Name, string Email, DateTimeOffset CreatedAt)
{
    public static UserDto From(User user) => new(user.Id, user.Name, user.Email, user.CreatedAt);
}

public sealed class UpdateProfileRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;

    public string NewPassword { get; init; } = string.Empty;
}

public sealed class DeleteAccountRequest
{
    public string Password { get; init; } = string.Empty;
}
