import { describe, expect, it } from 'vitest'
import { formatBadge, invitationStatusLabel, notificationRoute, notificationText, relativeTime } from '../messages'
import { makeInvitationNotification, makeNotification } from './fixtures'

// Intl pode usar espaço não separável entre "R$" e o valor.
const nbsp = (s: string) => s.replace(/ /g, ' ')

describe('notificationText', () => {
  it('nova despesa', () => {
    expect(nbsp(notificationText(makeNotification()))).toBe(
      'Brendo lançou uma nova despesa em Casa: Mercado (R$ 300,00)',
    )
  })

  it('despesa recorrente (uma notificação por série) mostra a quantidade e o valor de cada ocorrência', () => {
    const n = makeNotification({
      transaction: { id: 't3', description: 'IPTU', type: 'Expense', amount: 500, seriesOccurrences: 10 },
    })
    expect(nbsp(notificationText(n))).toBe('Brendo lançou uma despesa recorrente em Casa: IPTU (10x de R$ 500,00)')
  })

  it('série com 1 ocorrência ou sem `seriesOccurrences` usa o texto de despesa simples', () => {
    const single = makeNotification({
      transaction: { id: 't3', description: 'IPTU', type: 'Expense', amount: 500, seriesOccurrences: 1 },
    })
    expect(nbsp(notificationText(single))).toBe('Brendo lançou uma nova despesa em Casa: IPTU (R$ 500,00)')
  })

  it('pagamento usa "pagou" e o valor TOTAL pago (principal + juros)', () => {
    const n = makeNotification({
      type: 'GroupPaymentRegistered',
      transaction: { id: 't1', description: 'Mercado', type: 'Expense', amount: 523.5, seriesOccurrences: null },
    })
    expect(nbsp(notificationText(n))).toBe('Brendo pagou R$ 523,50 de Mercado em Casa')
  })

  it('nunca fala em receita nem "recebeu"', () => {
    for (const type of ['GroupTransactionCreated', 'GroupPaymentRegistered'] as const) {
      const text = notificationText(makeNotification({ type }))
      expect(text).not.toMatch(/receita|recebeu/i)
    }
  })

  it('convite', () => {
    expect(notificationText(makeInvitationNotification())).toBe('Ana convidou você para o grupo Casa')
  })

  it('ator ausente ou conta excluída vira "Usuário removido"', () => {
    expect(notificationText(makeNotification({ actor: null }))).toContain('Usuário removido lançou uma nova despesa')
    expect(notificationText(makeNotification({ actor: { userId: 'u9', name: 'Usuário removido' } }))).toContain(
      'Usuário removido lançou',
    )
  })
})

describe('notificationRoute', () => {
  it('movimentação abre a aba de movimentações do grupo com o detalhe (`open`)', () => {
    expect(notificationRoute(makeNotification())).toEqual({
      name: 'group-detail',
      params: { id: 'g1' },
      query: { tab: 'transactions', open: 't1' },
    })
  })

  it('convite não navega', () => {
    expect(notificationRoute(makeInvitationNotification())).toBeNull()
  })
})

describe('invitationStatusLabel', () => {
  it('rotula os estados finais e não rotula Pending', () => {
    expect(invitationStatusLabel('Pending')).toBeNull()
    expect(invitationStatusLabel('Accepted')).toBe('Convite aceito')
    expect(invitationStatusLabel('Declined')).toBe('Convite recusado')
    expect(invitationStatusLabel('Expired')).toBe('Convite expirado')
    expect(invitationStatusLabel('Revoked')).toBe('Convite cancelado')
  })
})

describe('relativeTime', () => {
  const now = new Date('2026-11-21T12:00:00Z').getTime()
  it.each([
    ['2026-11-21T11:59:40Z', 'agora'],
    ['2026-11-21T11:55:00Z', 'há 5 min'],
    ['2026-11-21T09:00:00Z', 'há 3 h'],
    ['2026-11-20T11:00:00Z', 'há 1 dia'],
    ['2026-11-11T12:00:00Z', 'há 10 dias'],
    ['2026-11-21T12:05:00Z', 'agora'],
    ['invalido', '—'],
  ])('%s -> %s', (iso, expected) => {
    expect(relativeTime(iso, now)).toBe(expected)
  })

  it('acima de 30 dias mostra a data completa', () => {
    expect(relativeTime('2026-09-01T12:00:00Z', now)).toMatch(/^\d{2}\/\d{2}\/\d{4}/)
  })
})

describe('formatBadge', () => {
  it('limita a 99+', () => {
    expect(formatBadge(5)).toBe('5')
    expect(formatBadge(99)).toBe('99')
    expect(formatBadge(100)).toBe('99+')
  })
})
