using FinanceApp.Application.Common.Options;
using FinanceApp.Infrastructure.Time;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace FinanceApp.UnitTests.Financial;

/// <summary>D-021: "hoje" usa o fuso configurado em App__TimeZone, validado na inicialização.</summary>
public class TimeZoneTests
{
    private static AppOptions Valid(string? timeZone = null) => new()
    {
        FrontendBaseUrl = "http://front.test",
        TimeZone = timeZone ?? AppOptions.DefaultTimeZone,
    };

    [Fact]
    public void DefaultTimeZone_IsSaoPaulo() => Assert.Equal("America/Sao_Paulo", new AppOptions().TimeZone);

    [Theory]
    [InlineData("America/Sao_Paulo")]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    public void Validate_AcceptsKnownTimeZones(string id) => Assert.Empty(Valid(id).Validate());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Nao/Existe")]
    [InlineData("São Paulo")]
    public void Validate_RejectsUnknownTimeZones(string id)
    {
        var errors = Valid(id).Validate().ToList();
        var error = Assert.Single(errors);
        Assert.Contains("App:TimeZone", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_FailsFastWithInvalidTimeZone() =>
        Assert.Throws<InvalidOperationException>(() => new SystemDateTimeProvider(MsOptions.Create(Valid("Nao/Existe"))));

    [Fact]
    public void Provider_TodayIsComputedInTheConfiguredZone_NotInUtc()
    {
        var saoPaulo = new SystemDateTimeProvider(MsOptions.Create(Valid("America/Sao_Paulo")));
        var kiritimati = new SystemDateTimeProvider(MsOptions.Create(Valid("Pacific/Kiritimati"))); // UTC+14
        var pagoPago = new SystemDateTimeProvider(MsOptions.Create(Valid("Pacific/Pago_Pago"))); // UTC-11

        // Os fusos extremos diferem entre si em até 1 dia e Today nunca é "a data UTC" por definição.
        var utcToday = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.InRange(kiritimati.Today.DayNumber - pagoPago.Today.DayNumber, 0, 2);
        Assert.InRange(Math.Abs(saoPaulo.Today.DayNumber - utcToday.DayNumber), 0, 1);
        Assert.True(kiritimati.Today >= saoPaulo.Today);
        Assert.True(saoPaulo.Today >= pagoPago.Today);
    }

    [Fact]
    public void Provider_UtcNow_IsUtcAndMillisecondPrecision()
    {
        var provider = new SystemDateTimeProvider(MsOptions.Create(Valid()));
        var now = provider.UtcNow;
        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.Equal(0, now.Ticks % TimeSpan.TicksPerMillisecond);
    }
}
