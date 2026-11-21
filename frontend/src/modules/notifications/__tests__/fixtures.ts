import type { NotificationDto } from '../types'

/** Fixtures usadas SOMENTE em testes (nunca importar em código de produção). */
export function makeNotification(partial: Partial<NotificationDto> = {}): NotificationDto {
  return {
    id: 'n1',
    type: 'GroupTransactionCreated',
    createdAt: '2026-11-21T10:00:00Z',
    readAt: null,
    isRead: false,
    group: { id: 'g1', name: 'Casa' },
    actor: { userId: 'u2', name: 'Brendo' },
    transaction: { id: 't1', description: 'Mercado', type: 'Expense', amount: 300, seriesOccurrences: null },
    invitation: null,
    ...partial,
  }
}

export function makeInvitationNotification(
  partial: Partial<NotificationDto> = {},
  invitation: Partial<NonNullable<NotificationDto['invitation']>> = {},
): NotificationDto {
  return makeNotification({
    id: 'n-inv',
    type: 'GroupInvitationReceived',
    actor: { userId: 'u3', name: 'Ana' },
    transaction: null,
    invitation: { id: 'i1', status: 'Pending', expiresAt: '2026-12-01T00:00:00Z', canRespond: true, ...invitation },
    ...partial,
  })
}

export function makePage(
  items: NotificationDto[],
  extra: Partial<{ page: number; totalPages: number; totalCount: number }> = {},
) {
  return {
    items,
    page: extra.page ?? 1,
    pageSize: 20,
    totalCount: extra.totalCount ?? items.length,
    totalPages: extra.totalPages ?? 1,
  }
}
