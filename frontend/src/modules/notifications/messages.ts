import type { RouteLocationRaw } from 'vue-router'
import type { GroupInvitationStatus } from '@/types/api'
import { formatCurrency, formatDateTime } from '@/utils/format'
import type { NotificationDto } from './types'

export const REMOVED_USER_LABEL = 'Usuário removido'

export function actorName(notification: NotificationDto): string {
  return notification.actor?.name?.trim() || REMOVED_USER_LABEL
}

/** Texto pt-BR da notificação, montado a partir dos campos estruturados (D-042). */
export function notificationText(n: NotificationDto): string {
  const actor = actorName(n)
  const group = n.group.name

  if (n.type === 'GroupInvitationReceived') {
    return `${actor} convidou você para o grupo ${group}`
  }

  const tx = n.transaction
  const description = tx?.description ?? 'uma movimentação'
  const amount = tx ? formatCurrency(tx.amount) : ''

  if (n.type === 'GroupTransactionCreated') {
    if (tx?.seriesOccurrences && tx.seriesOccurrences > 1) {
      return `${actor} lançou uma despesa recorrente em ${group}: ${description} (${tx.seriesOccurrences}x de ${amount})`
    }
    return `${actor} lançou uma nova despesa em ${group}: ${description}${tx ? ` (${amount})` : ''}`
  }

  // GroupPaymentRegistered: `amount` é o total pago (principal + juros).
  return `${actor} pagou${tx ? ` ${amount}` : ''} de ${description} em ${group}`
}

const INVITATION_STATUS_LABELS: Record<GroupInvitationStatus, string | null> = {
  Pending: null,
  Accepted: 'Convite aceito',
  Declined: 'Convite recusado',
  Expired: 'Convite expirado',
  Revoked: 'Convite cancelado',
}

/** Rótulo do convite quando já não está pendente; `null` se `Pending`. */
export function invitationStatusLabel(status: GroupInvitationStatus): string | null {
  return INVITATION_STATUS_LABELS[status]
}

/** Destino ao clicar: movimentação → aba de movimentações do grupo com o detalhe aberto (`?open=`). */
export function notificationRoute(n: NotificationDto): RouteLocationRaw | null {
  if (n.type === 'GroupInvitationReceived' || !n.transaction) return null
  return {
    name: 'group-detail',
    params: { id: n.group.id },
    query: { tab: 'transactions', open: n.transaction.id },
  }
}

/** Tempo relativo curto ("há 5 min"). A data completa fica no `title` (use `formatDateTime`). */
export function relativeTime(iso: string, now: number = Date.now()): string {
  const time = new Date(iso).getTime()
  if (Number.isNaN(time)) return '—'
  const seconds = Math.floor((now - time) / 1000)
  if (seconds < 60) return 'agora'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `há ${minutes} min`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `há ${hours} h`
  const days = Math.floor(hours / 24)
  if (days < 30) return `há ${days} ${days === 1 ? 'dia' : 'dias'}`
  return formatDateTime(iso)
}

/** Contador exibido no sino: 99+ acima de 99. */
export function formatBadge(count: number): string {
  return count > 99 ? '99+' : String(count)
}
