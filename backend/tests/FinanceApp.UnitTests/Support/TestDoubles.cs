using FinanceApp.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.UnitTests.Support;

internal sealed class FakeClock(DateTimeOffset start) : IDateTimeProvider
{
    private DateOnly? todayOverride;

    public DateTimeOffset UtcNow { get; private set; } = start;

    /// <summary>"Hoje" no fuso da aplicação; por padrão derivado do UTC (UTC-3 como America/Sao_Paulo) ou fixado por <see cref="SetToday"/>.</summary>
    public DateOnly Today => todayOverride ?? DateOnly.FromDateTime(UtcNow.ToOffset(TimeSpan.FromHours(-3)).DateTime);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);

    public void SetToday(DateOnly today) => todayOverride = today;
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

internal static class TestDb
{
    /// <summary>DbContext em memória com o seed das categorias padrão (HasData) aplicado.</summary>
    public static FinanceApp.Infrastructure.Persistence.AppDbContext Create()
    {
        var db = new FinanceApp.Infrastructure.Persistence.AppDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<FinanceApp.Infrastructure.Persistence.AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        db.Database.EnsureCreated();
        return db;
    }
}
