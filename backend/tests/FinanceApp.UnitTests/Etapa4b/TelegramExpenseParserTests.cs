using FinanceApp.Application.Telegram;

namespace FinanceApp.UnitTests.Etapa4b;

public class TelegramExpenseParserTests
{
    [Theory]
    [InlineData("45,90 mercado", 45.90, "mercado")]
    [InlineData("mercado 45,90", 45.90, "mercado")]
    [InlineData("45.90 mercado", 45.90, "mercado")]
    [InlineData("R$ 45,90 mercado do bairro", 45.90, "mercado do bairro")]
    [InlineData("R$45,90 padaria", 45.90, "padaria")]
    [InlineData("/despesa 1.234,56 aluguel", 1234.56, "aluguel")]
    [InlineData("/despesa@meubot 10 café", 10, "café")]
    [InlineData("aluguel 1.234,56", 1234.56, "aluguel")]
    [InlineData("1.500 reforma", 1500, "reforma")]
    [InlineData("12 pão", 12, "pão")]
    [InlineData("7,5 estacionamento", 7.5, "estacionamento")]
    public void Parse_Valid(string text, double amount, string description)
    {
        var parsed = TelegramExpenseParser.Parse(text);
        Assert.NotNull(parsed);
        Assert.Equal((decimal)amount, parsed!.Amount);
        Assert.Equal(description, parsed.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("oi")]
    [InlineData("45,90")]
    [InlineData("mercado")]
    [InlineData("0 nada")]
    [InlineData("2 pizzas 45,90")]
    [InlineData("-5 estorno")]
    [InlineData("45,999 mercado")]
    public void Parse_Invalid(string? text) => Assert.Null(TelegramExpenseParser.Parse(text));
}
