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

  it('nova receita', () => {
    const n = makeNotification({
      transaction: { id: 't2', description: 'Aluguel', type: 'Income', amount: 1500.5 },
    })
    expect(nbsp(notificationText(n))).toBe('Brendo lançou uma nova receita em Casa: Aluguel (R$ 1.500,50)')
  })

  it('pagamento de despesa usa "pagou" e o valor do pagamento', () => {
    const n = makeNotification({
      type: 'GroupPaymentRegistered',
      transaction: { id: 't1', description: 'Mercado', type: 'Expense', amount: 100 },
    })
    expect(nbsp(notificationText(n))).toBe('Brendo pagou R$ 100,00 de Mercado em Casa')
  })

  it('pagamento de receita usa "recebeu"', () => {
    const n = makeNotification({
      type: 'GroupPaymentRegistered',
      transaction: { id: 't2', description: 'Aluguel', type: 'Income', amount: 100 },
    })
    expect(nbsp(notificationText(n))).toBe('Brendo recebeu R$ 100,00 de Aluguel em Casa')
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
