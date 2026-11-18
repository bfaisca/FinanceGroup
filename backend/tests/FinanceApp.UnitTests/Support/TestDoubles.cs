using FinanceApp.Application.Common.Abstractions;

namespace FinanceApp.UnitTests.Support;

internal sealed class FakeClock(DateTimeOffset start) : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

internal sealed class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("SMTP indisponível");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }

    public Guid? SessionId { get; set; }

    public Guid RequireUserId() => UserId ?? throw Application.Common.Exceptions.UnauthorizedException.NotAuthenticated();
}
