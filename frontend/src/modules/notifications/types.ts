/**
 * Tipos espelhando docs/API_CONTRACTS.md §17 (Etapa 4b — Notificações). Não adicionar campos fora do contrato.
 */
import type { GroupInvitationStatus, IsoDateTimeString, TransactionType, Uuid } from '@/types/api'

export type NotificationType = 'GroupTransactionCreated' | 'GroupPaymentRegistered' | 'GroupInvitationReceived'

export interface NotificationDto {
  id: Uuid
  type: NotificationType
  createdAt: IsoDateTimeString
  readAt: IsoDateTimeString | null
  isRead: boolean
  group: { id: Uuid; name: string }
  /** "Usuário removido" se a conta foi excluída. */
  actor: { userId: Uuid; name: string } | null
  /** `null` em convites. */
  transaction: {
    id: Uuid
    description: string
    /** Tipo da movimentação. */
    type: TransactionType
    /** `GroupTransactionCreated`: valor da movimentação; `GroupPaymentRegistered`: valor do pagamento. */
    amount: number
  } | null
  /** Somente em `GroupInvitationReceived`. */
  invitation: {
    id: Uuid
    status: GroupInvitationStatus
    expiresAt: IsoDateTimeString
    /** Pending + não expirado + e-mail da conta = e-mail do convite + não é membro. */
    canRespond: boolean
  } | null
}

export interface NotificationListQuery {
  page?: number
  /** Padrão 20, máx. 50. */
  pageSize?: number
  /** Padrão `false`. */
  unreadOnly?: boolean
}

export interface UnreadCountDto {
  count: number
}
