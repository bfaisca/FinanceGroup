import { describe, expect, it } from 'vitest'
import type { TransactionHistoryEntryDto } from '@/types/api'
import { describeHistoryEntry, formatHistoryValue } from '../history'

function entry(partial: Partial<TransactionHistoryEntryDto>): TransactionHistoryEntryDto {
  return {
    id: '1',
    action: 'Updated',
    field: null,
    oldValue: null,
    newValue: null,
    createdAt: '2026-11-18T22:40:00Z',
    ...partial,
  }
}

describe('histórico em linguagem legível', () => {
  it('descreve alteração de valor com moeda', () => {
    const text = describeHistoryEntry(entry({ field: 'amount', oldValue: '100.00', newValue: '150.5' }))
    expect(text).toContain('Valor')
    expect(text).toMatch(/R\$\s100,00/)
    expect(text).toMatch(/R\$\s150,50/)
  })

  it('formata datas sem deslocar o dia', () => {
    expect(formatHistoryValue('dueDate', '2026-11-18')).toBe('18/11/2026')
  })

  it('traduz situação e método de pagamento', () => {
    expect(formatHistoryValue('status', 'Overdue')).toBe('Atrasado')
    expect(formatHistoryValue('paymentMethod', 'Pix')).toBe('Pix')
    expect(formatHistoryValue('paymentMethod', 'BankSlip')).toBe('Boleto')
  })

  it('resolve identificadores de categoria e conta quando possível', () => {
    const resolvers = { category: (id: string) => (id === 'c1' ? 'Moradia' : undefined) }
    expect(formatHistoryValue('categoryId', 'c1', resolvers)).toBe('Moradia')
    expect(formatHistoryValue('categoryId', 'c2', resolvers)).toBe('c2')
  })

  it('usa "vazio" para valores nulos e títulos para ações sem campo', () => {
    expect(formatHistoryValue('notes', null)).toBe('vazio')
    expect(describeHistoryEntry(entry({ action: 'Created' }))).toBe('Movimentação criada')
    expect(describeHistoryEntry(entry({ action: 'Canceled' }))).toBe('Movimentação cancelada')
    expect(describeHistoryEntry(entry({ action: 'Reopened' }))).toBe('Movimentação reaberta')
  })

  it('descreve pagamentos registrados/removidos', () => {
    expect(describeHistoryEntry(entry({ action: 'PaymentAdded', field: 'amount', newValue: '50' }))).toMatch(
      /Pagamento registrado \(Valor: R\$\s50,00\)/,
    )
    expect(describeHistoryEntry(entry({ action: 'PaymentRemoved', field: 'amount', oldValue: '50' }))).toMatch(
      /Pagamento removido/,
    )
  })

  it('mantém o nome do campo desconhecido', () => {
    expect(describeHistoryEntry(entry({ field: 'algoNovo', oldValue: 'a', newValue: 'b' }))).toBe(
      'algoNovo: de a para b',
    )
  })
  it('descreve pagamento alterado (de/para), inclusive com juros/multa', () => {
    const updated = entry({
      action: 'PaymentUpdated',
      field: 'payment',
      oldValue: '500.00 (2026-11-10)',
      newValue: '500.00 + 23.50 (2026-11-10)',
    })
    expect(describeHistoryEntry(updated)).toMatch(
      /^Pagamento alterado: de R\$\s500,00 em 10\/11\/2026 para R\$\s500,00 \+ R\$\s23,50 de juros\/multa em 10\/11\/2026$/,
    )
    // Formato desconhecido: mostra o texto como veio.
    expect(
      describeHistoryEntry(entry({ action: 'PaymentUpdated', field: 'payment', oldValue: 'a', newValue: 'b' })),
    ).toBe('Pagamento alterado: de a para b')
  })

  it('descreve pagamentos registrados e removidos no formato do backend', () => {
    const added = entry({ action: 'PaymentAdded', field: 'payment', newValue: '200.00 (2026-11-18)' })
    const removed = entry({ action: 'PaymentRemoved', field: 'payment', oldValue: '1234.50 (2026-11-02)' })
    expect(describeHistoryEntry(added)).toMatch(/^Pagamento registrado: R\$\s200,00 em 18\/11\/2026$/)
    expect(describeHistoryEntry(removed)).toMatch(/^Pagamento removido: R\$\s1\.234,50 em 02\/11\/2026$/)
    expect(describeHistoryEntry(entry({ action: 'PaymentAdded', field: 'payment', newValue: 'formato novo' }))).toBe(
      'Pagamento registrado',
    )
  })
})
