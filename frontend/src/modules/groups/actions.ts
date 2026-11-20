import type { GroupTransactionDto } from './types'

export interface GroupTransactionActions {
  canEdit: boolean
  canPay: boolean
  canCancel: boolean
  canReopen: boolean
  canDelete: boolean
  /** Há pagamentos: excluir não é possível, só cancelar (D-020). */
  deleteBlockedByPayments: boolean
}

/**
 * Ações oferecidas na movimentação do grupo. A autorização vem SEMPRE do backend (`permissions`):
 * aqui só se afunila por estado (ex.: não cancelar o que já está cancelado). O contrato não tem
 * um campo próprio para reabrir; como é restrito ao criador (D-032), usa-se qualquer permissão de criador.
 */
export function groupTransactionActions(
  tx: Pick<GroupTransactionDto, 'status' | 'permissions' | 'paidAmount' | 'remainingAmount'>,
): GroupTransactionActions {
  const canceled = tx.status === 'Canceled'
  const { canEdit, canCancel, canDelete, canPay } = tx.permissions
  return {
    canEdit: canEdit && !canceled,
    canPay: canPay && !canceled && tx.remainingAmount > 0,
    canCancel: canCancel && !canceled,
    canReopen: canceled && (canCancel || canEdit || canDelete),
    canDelete,
    deleteBlockedByPayments: canDelete === false && tx.paidAmount > 0 && (canCancel || canEdit),
  }
}
