using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Transactions;

/// <summary>
/// Pagamentos de grupo feitos pelo usuário com uma conta própria (D-033), para exibição na listagem individual (D-060).
/// Apenas leitura: não são movimentações individuais e não entram em totais do dashboard/relatórios.
/// </summary>
internal sealed class GroupPaymentEntries(IAppDbContext db)
{
    /// <summary>Filtros sem equivalente em pagamentos de grupo (vencimento, cartão, parcelamento, recorrência) os excluem.</summary>
    public static bool AppliesTo(TransactionListQuery q) =>
        q.DueFrom is null && q.DueTo is null && q.CreditCardId is null && q.InstallmentPlanId is null && q.RecurrenceRuleId is null
        && q.Status is null or TransactionStatus.Paid or TransactionStatus.Received;

    public async Task<List<TransactionDto>> ListAsync(Guid userId, TransactionListQuery q, CancellationToken cancellationToken)
    {
        if (!AppliesTo(q))
        {
            return [];
        }

        var rows = from p in db.TransactionPayments.AsNoTracking()
                   join t in db.Transactions.AsNoTracking() on p.TransactionId equals t.Id
                   join g in db.Groups.AsNoTracking() on t.GroupId equals g.Id
                   where p.CreatedByUserId == userId && p.AccountId != null && t.GroupId != null && t.Status != TransactionStatus.Canceled
                   select new { p, t, GroupName = g.Name };

        if (q.From is { } from)
        {
            rows = rows.Where(x => x.p.PaymentDate >= from);
        }

        if (q.To is { } to)
        {
            rows = rows.Where(x => x.p.PaymentDate <= to);
        }

        if (q.Type is { } type)
        {
            rows = rows.Where(x => x.t.Type == type);
        }

        if (q.CategoryId is { } categoryId)
        {
            rows = rows.Where(x => x.t.CategoryId == categoryId || x.t.Category!.ParentCategoryId == categoryId);
        }

        if (q.AccountId is { } accountId)
        {
            rows = rows.Where(x => x.p.AccountId == accountId);
        }

        // Pagamentos são "pagos/recebidos": o filtro de situação só combina com o tipo correspondente.
        if (q.Status == TransactionStatus.Paid)
        {
            rows = rows.Where(x => x.t.Type == TransactionType.Expense);
        }
        else if (q.Status == TransactionStatus.Received)
        {
            rows = rows.Where(x => x.t.Type == TransactionType.Income);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLowerInvariant();
#pragma warning disable CA1862, CA1304, CA1311 // Traduzido para SQL.
            rows = rows.Where(x => x.t.Description.ToLower().Contains(term));
#pragma warning restore CA1311, CA1304, CA1862
        }

        var data = await rows
            .Select(x => new
            {
                PaymentId = x.p.Id,
                x.p.Amount,
                x.p.InterestAmount,
                x.p.PaymentDate,
                x.p.AccountId,
                AccountName = x.p.Account == null ? null : x.p.Account.Name,
                x.p.PaymentMethod,
                PaymentCreatedAt = x.p.CreatedAt,
                PaymentUpdatedAt = x.p.UpdatedAt,
                TransactionId = x.t.Id,
                x.t.Description,
                x.t.Type,
                x.t.CategoryId,
                CategoryName = x.t.Category!.Name,
                ParentCategoryName = x.t.Category.ParentCategory == null ? null : x.t.Category.ParentCategory.Name,
                CategoryColor = x.t.Category.Color,
                CategoryIcon = x.t.Category.Icon,
                GroupId = x.t.GroupId!.Value,
                x.GroupName,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return data.Select(d =>
        {
            var total = d.Amount + d.InterestAmount;
            return new TransactionDto(
                d.PaymentId,
                d.Description,
                d.Type,
                total,
                total,
                0m,
                d.Type == TransactionType.Expense ? TransactionStatus.Paid : TransactionStatus.Received,
                d.CategoryId,
                d.CategoryName,
                d.ParentCategoryName,
                d.CategoryColor,
                d.CategoryIcon,
                d.AccountId,
                d.AccountName,
                d.PaymentMethod,
                d.PaymentDate,
                null,
                d.PaymentDate,
                null,
                d.PaymentCreatedAt,
                d.PaymentUpdatedAt,
                null,
                null,
                null,
                null,
                new GroupPaymentRefDto(d.GroupId, d.GroupName, d.TransactionId));
        }).ToList();
    }

    /// <summary>Ordem do contrato (<c>sort</c>) aplicada em memória, espelhando o banco (nulos por último em ASC).</summary>
    public static IEnumerable<TransactionDto> Sort(IEnumerable<TransactionDto> items, string? sort)
    {
        var spec = string.IsNullOrWhiteSpace(sort) ? TransactionListQuery.DefaultSort : sort.Trim();
        var descending = spec.StartsWith('-');
        var field = spec.TrimStart('-').ToLowerInvariant();

        IComparable? Key(TransactionDto t) => field switch
        {
            "duedate" => t.DueDate,
            "amount" => t.Amount,
            "description" => t.Description.ToLowerInvariant(),
            "createdat" => t.CreatedAt,
            _ => t.TransactionDate,
        };

        // Em "transactionDate" o padrão é descendente quando o campo é desconhecido (igual ao banco).
        if (field is not ("transactiondate" or "duedate" or "amount" or "description" or "createdat"))
        {
            descending = true;
        }

        var comparer = Comparer<IComparable?>.Create((a, b) =>
            a is null ? (b is null ? 0 : 1) : b is null ? -1 : a.CompareTo(b));

        var ordered = descending
            ? items.OrderByDescending(Key, comparer)
            : items.OrderBy(Key, comparer);
        return ordered.ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id);
    }
}
