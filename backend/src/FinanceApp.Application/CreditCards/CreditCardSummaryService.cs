using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.CreditCards;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.CreditCards;

/// <summary>Regras puras de limite (D-025).</summary>
public static class CreditCardLimits
{
    /// <summary><c>limite disponível = limite − utilizado</c> (negativo = limite excedido).</summary>
    public static decimal Available(decimal creditLimit, decimal usedLimit) => creditLimit - usedLimit;
}

/// <summary>
/// Limite utilizado e fatura aberta (D-025), com agregação no banco. Os cartões recebidos já devem ter sido
/// filtrados pelo usuário autenticado pelo chamador.
/// <list type="bullet">
/// <item>Utilizado = soma de <c>amount − paidAmount</c> de TODAS as compras/parcelas não canceladas do cartão (inclui parcelas futuras).</item>
/// <item>Fatura aberta = a que recebe compras feitas hoje; valor = soma do restante das compras não canceladas com esse vencimento.</item>
/// </list>
/// </summary>
public sealed class CreditCardSummaryService(IAppDbContext db)
{
    public async Task<IReadOnlyDictionary<Guid, CreditCardSummary>> GetAsync(
        IReadOnlyCollection<CreditCard> cards,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, CreditCardSummary>(cards.Count);
        if (cards.Count == 0)
        {
            return result;
        }

        var ids = cards.Select(c => c.Id).ToList();
        var invoices = cards.ToDictionary(c => c.Id, c => c.GetInvoice(today));
        var dueDates = invoices.Values.Select(i => i.DueDate).Distinct().ToList();

        var used = await db.Transactions
            .AsNoTracking()
            .Where(t => t.CreditCardId != null && ids.Contains(t.CreditCardId.Value) && t.Status != TransactionStatus.Canceled)
            .GroupBy(t => t.CreditCardId!.Value)
            .Select(g => new { CardId = g.Key, Used = g.Sum(t => t.Amount - t.PaidAmount) })
            .ToDictionaryAsync(x => x.CardId, x => x.Used, cancellationToken)
            .ConfigureAwait(false);

        var invoiceRows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.CreditCardId != null && ids.Contains(t.CreditCardId.Value) && t.Status != TransactionStatus.Canceled
                        && t.DueDate != null && dueDates.Contains(t.DueDate.Value))
            .GroupBy(t => new { CardId = t.CreditCardId!.Value, DueDate = t.DueDate!.Value })
            .Select(g => new { g.Key.CardId, g.Key.DueDate, Amount = g.Sum(t => t.Amount - t.PaidAmount) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var card in cards)
        {
            var invoice = invoices[card.Id];
            var usedLimit = used.GetValueOrDefault(card.Id);
            var invoiceAmount = invoiceRows.FirstOrDefault(r => r.CardId == card.Id && r.DueDate == invoice.DueDate)?.Amount ?? 0m;
            result[card.Id] = new CreditCardSummary(
                usedLimit,
                CreditCardLimits.Available(card.CreditLimit, usedLimit),
                new OpenInvoiceDto(invoice.ClosingDate, invoice.DueDate, invoiceAmount));
        }

        return result;
    }
}
