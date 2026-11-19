import type { TransactionDto } from '@/types/api'

export interface TransactionActions {
  canEdit: boolean
  canPay: boolean
  canCancel: boolean
  canReopen: boolean
  canDelete: boolean
  /** Há pagamentos: excluir não é possível, só cancelar (D-020). */
  deleteBlockedByPayments: boolean
  canRemovePayments: boolean
}

/**
 * Ações oferecidas por estado (docs/STATE_MACHINE.md). É só UX: o backend valida cada transição
 * e o `422 business_rule_violation` sempre é tratado na tela.
 */
export function availableActions(
  tx: Pick<TransactionDto, 'status' | 'paidAmount' | 'remainingAmount'>,
): TransactionActions {
  const canceled = tx.status === 'Canceled'
  const hasPayments = tx.paidAmount > 0
  return {
    canEdit: !canceled,
    canPay: !canceled && tx.remainingAmount > 0,
    canCancel: !canceled,
    canReopen: canceled,
    canDelete: !hasPayments,
    deleteBlockedByPayments: hasPayments,
    canRemovePayments: !canceled,
  }
}

/** Percentual pago (0–100), calculado em centavos para evitar erro de ponto flutuante. */
export function paidPercentage(paidAmount: number, amount: number): number {
  const total = Math.round(amount * 100)
  if (total <= 0) return 0
  const paid = Math.max(0, Math.round(paidAmount * 100))
  return Math.min(100, Math.round((paid / total) * 100))
}
