import { describe, expect, it } from 'vitest'
import {
  countActiveFilters,
  defaultFilters,
  mergeRouteQuery,
  parseTransactionFilters,
  toListQuery,
  toRouteQuery,
} from '../filters'

describe('filtros ↔ query string', () => {
  it('sem parâmetros: filtros padrão e URL vazia', () => {
    const filters = parseTransactionFilters({})
    expect(filters).toEqual(defaultFilters())
    expect(toRouteQuery(filters)).toEqual({})
  })

  it('faz o round-trip de todos os filtros', () => {
    const query = {
      from: '2026-11-01',
      to: '2026-11-30',
      type: 'Expense',
      status: 'Overdue',
      categoryId: 'c1',
      accountId: 'a1',
      search: 'mercado',
      page: '3',
      pageSize: '50',
      sort: 'amount',
    }
    const filters = parseTransactionFilters(query)
    expect(filters).toMatchObject({
      from: '2026-11-01',
      to: '2026-11-30',
      type: 'Expense',
      status: 'Overdue',
      categoryId: 'c1',
      accountId: 'a1',
      search: 'mercado',
      page: 3,
      pageSize: 50,
      sort: 'amount',
    })
    expect(toRouteQuery(filters)).toEqual(query)
  })

  it('ignora valores inválidos sem quebrar', () => {
    const filters = parseTransactionFilters({
      from: '2026-13-45',
      to: 'ontem',
      type: 'Transfer',
      status: 'Foo',
      page: '-2',
      pageSize: 'abc',
      sort: 'password',
    })
    expect(filters).toEqual(defaultFilters())
  })

  it('limita pageSize a 100 e usa o primeiro valor de parâmetros repetidos', () => {
    const filters = parseTransactionFilters({ pageSize: '500', type: ['Income', 'Expense'] })
    expect(filters.pageSize).toBe(100)
    expect(filters.type).toBe('Income')
  })

  it('aceita ordenação descendente com prefixo', () => {
    expect(parseTransactionFilters({ sort: '-dueDate' }).sort).toBe('-dueDate')
  })

  it('omite valores padrão da URL', () => {
    const query = toRouteQuery(defaultFilters({ page: 1, pageSize: 20, sort: '-transactionDate', type: 'Income' }))
    expect(query).toEqual({ type: 'Income' })
  })

  it('toListQuery envia paginação, ordenação e filtros no formato do contrato', () => {
    expect(toListQuery(defaultFilters({ status: 'Overdue', search: ' luz ', accountId: 'a1' }))).toEqual({
      page: 1,
      pageSize: 20,
      sort: '-transactionDate',
      status: 'Overdue',
      search: 'luz',
      accountId: 'a1',
    })
  })

  it('mergeRouteQuery preserva `open` e `new`', () => {
    const merged = mergeRouteQuery({ open: 't1', new: '1', search: 'x' }, defaultFilters({ type: 'Income' }))
    expect(merged).toEqual({ open: 't1', new: '1', type: 'Income' })
  })

  it('countActiveFilters ignora paginação e ordenação', () => {
    expect(countActiveFilters(defaultFilters({ page: 3, sort: 'amount' }))).toBe(0)
    expect(countActiveFilters(defaultFilters({ type: 'Income', search: 'a', from: '2026-11-01' }))).toBe(3)
  })
})

describe('filtros da Etapa 3 (cartão, plano, regra)', () => {
  const query = { creditCardId: 'cc1', installmentPlanId: 'p1', recurrenceRuleId: 'r1' }

  it('lê da URL, envia ao backend e devolve à URL', () => {
    const filters = parseTransactionFilters(query)
    expect(filters).toMatchObject(query)
    expect(toListQuery(filters)).toMatchObject(query)
    expect(toRouteQuery(filters)).toEqual(query)
    expect(countActiveFilters(filters)).toBe(3)
  })

  it('combina com os demais filtros e preserva `open`/`new`', () => {
    const filters = parseTransactionFilters({ creditCardId: 'cc1', status: 'Overdue' })
    expect(mergeRouteQuery({ open: 't1' }, filters)).toEqual({ open: 't1', creditCardId: 'cc1', status: 'Overdue' })
  })

  it('ignora valores vazios', () => {
    const filters = parseTransactionFilters({ creditCardId: '  ' })
    expect(filters.creditCardId).toBeUndefined()
    expect(toRouteQuery(filters)).toEqual({})
  })
})
