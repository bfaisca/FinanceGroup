# Máquina de estados das movimentações

Persistidos: `Planned`, `Pending`, `Paid`, `Received`, `Canceled`. **`Overdue` nunca é persistido**: é calculado na leitura (D-009).

## Definições
- `A` = valor da movimentação. `S` = soma dos pagamentos. `restante = A − S`. Sempre `0 ≤ S ≤ A`.
- Liquidada: `S == A`. Tipo `Expense` → `Paid`; tipo `Income` → `Received`.
- `hoje` = data atual no fuso `App__TimeZone` (D-021).

## Estado persistido
| Condição | Estado |
|---|---|
| Cancelada explicitamente | `Canceled` |
| `S == A` (e não cancelada) | `Paid` (despesa) / `Received` (receita) |
| `0 < S < A` | `Pending` |
| `S == 0` | o valor escolhido pelo usuário: `Planned` ou `Pending` (padrão `Pending`); se estava liquidada e o pagamento foi removido, volta a `Pending` |

## Estado apresentado (`status` na API)
`Overdue` quando o estado persistido é `Planned` ou `Pending`, `restante > 0`, existe `dueDate` e `dueDate < hoje`. Caso contrário, igual ao persistido. Sem `dueDate` nunca é `Overdue`.

## Transições
| De | Ação | Para | Regras |
|---|---|---|---|
| (novo) | criar | `Planned` ou `Pending` | outros valores → 400 validation_error |
| `Planned`/`Pending` | registrar pagamento parcial | `Pending` | `0 < S < A` |
| `Planned`/`Pending` | registrar pagamento que completa | `Paid`/`Received` | `paidAt` = data do pagamento que completou |
| `Paid`/`Received` | remover pagamento | `Pending` | `paidAt` = null |
| `Pending`/`Planned` | remover pagamento (S continua > 0) | `Pending` | |
| qualquer ≠ `Canceled` | cancelar | `Canceled` | pagamentos preservados; fora de totais e do saldo projetado |
| `Canceled` | reabrir | estado derivado de `S` (tabela acima; com `S == 0` → `Pending`) | |
| `Canceled` | registrar/remover pagamento, editar | **proibido** | 422 `business_rule_violation` |
| qualquer | excluir | (removida) | só se não houver pagamentos (D-020) |

## Invariantes
1. `S ≤ A` (pagamento que ultrapassa → 422).
2. Pagamento com `paymentDate` no futuro → 422.
3. Editar `amount` para valor `< S` → 422. Após editar, o estado é recalculado.
4. `type` imutável.
5. `paidAt` só existe quando liquidada.
6. Cada transição relevante gera uma linha de histórico (`transaction_audit_entries`).

## Etapa 3 — origem das movimentações
- Parcelas e ocorrências de recorrência nascem `Planned` (data futura) ou `Pending` (data ≤ hoje) — D-026/D-027. Depois seguem exatamente a mesma máquina de estados (pagamentos, cancelar, reabrir, `Overdue` calculado).
- Compras no cartão seguem a máquina normal; `dueDate` é o vencimento da fatura (D-025), logo uma compra de cartão em aberto fica `Overdue` quando a fatura vence sem pagamento.
- Parcelas e ocorrências **não podem ser excluídas individualmente** (422), apenas canceladas.
