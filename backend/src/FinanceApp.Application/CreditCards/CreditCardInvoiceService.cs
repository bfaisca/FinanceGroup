using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.CreditCards;

/// <summary>Fatura com valor em aberto, calculada a partir das compras do cartão (D-025, D-057).</summary>
public sealed record CardInvoiceDto(DateOnly DueDate, decimal TotalAmount, decimal PaidAmount, decimal RemainingAmount, int PurchaseCount, bool IsOverdue);

public sealed class PayInvoiceRequest
{
    public DateOnly? DueDate { get; init; }

    public DateOnly? PaymentDate { get; init; }
}

public sealed record PayInvoiceResultDto(DateOnly DueDate, DateOnly PaymentDate, decimal PaidAmount, int PurchasesPaid);

/// <summary>
/// Pagamento de fatura (D-057): não existe entidade de fatura; "pagar a fatura" registra, numa única transação de banco,
/// o pagamento do valor restante de cada compra individual não cancelada do cartão com aquele vencimento.
/// Não cria despesa nova (D-025).
/// </summary>
public sealed class CreditCardInvoiceService(IAppDbContext db, ICurrentUser currentUser, IDateTimeProvider clock)
{
    public async Task<IReadOnlyList<CardInvoiceDto>> ListAsync(Guid cardId, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        await EnsureCardAsync(cardId, userId, cancellationToken).ConfigureAwait(false);
        var today = clock.Today;

        var rows = await db.Transactions
            .AsNoTracking()
            .Where(t => t.GroupId == null && t.CreditCardId == cardId && t.CreatedByUserId == userId
                        && t.Status != TransactionStatus.Canceled && t.DueDate != null)
            .GroupBy(t => t.DueDate!.Value)
            .Select(g => new { DueDate = g.Key, Total = g.Sum(t => t.Amount), Paid = g.Sum(t => t.PaidAmount), Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Where(r => r.Total - r.Paid > 0m)
            .OrderBy(r => r.DueDate)
            .Select(r => new CardInvoiceDto(r.DueDate, r.Total, r.Paid, r.Total - r.Paid, r.Count, r.DueDate < today))
            .ToList();
    }

    public async Task<PayInvoiceResultDto> PayAsync(Guid cardId, PayInvoiceRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        await EnsureCardAsync(cardId, userId, cancellationToken).ConfigureAwait(false);

        if (request.DueDate is not { } dueDate)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["dueDate"] = ["O vencimento da fatura é obrigatório."] });
        }

        var today = clock.Today;
        var paymentDate = request.PaymentDate ?? today;
        if (paymentDate > today)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["paymentDate"] = ["A data do pagamento não pode ser futura."] });
        }

        var purchases = await db.Transactions
            .AsTracking()
            .Where(t => t.GroupId == null && t.CreditCardId == cardId && t.CreatedByUserId == userId
                        && t.Status != TransactionStatus.Canceled && t.DueDate == dueDate && t.PaidAmount < t.Amount)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (purchases.Count == 0)
        {
            throw new DomainException("Não há valor em aberto nesta fatura.");
        }

        var total = 0m;
        foreach (var purchase in purchases)
        {
            var remaining = purchase.RemainingAmount;
            var payment = purchase.AddPayment(userId, remaining, paymentDate, today, null, purchase.PaymentMethod, clock.UtcNow);
            db.TransactionPayments.Add(payment);
            total += remaining;
        }

        // Concorrência otimista (xmin): se alguma compra mudou no meio, nada é gravado e o cliente recebe 409.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PayInvoiceResultDto(dueDate, paymentDate, total, purchases.Count);
    }

    private async Task EnsureCardAsync(Guid cardId, Guid userId, CancellationToken cancellationToken)
    {
        var exists = await db.CreditCards.AsNoTracking().AnyAsync(c => c.Id == cardId && c.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            throw new NotFoundException("Cartão de crédito não encontrado.");
        }
    }
}
