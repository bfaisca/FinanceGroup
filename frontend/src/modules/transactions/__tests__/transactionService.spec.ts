import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { toTransactionParams, transactionService } from '../services/transactionService'

describe('transactionService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('toTransactionParams remove vazios e faz trim da busca', () => {
    expect(
      toTransactionParams({
        page: 2,
        pageSize: 20,
        sort: '-amount',
        search: '  mercado  ',
        type: 'Expense',
        from: undefined,
        categoryId: '',
      }),
    ).toEqual({ page: 2, pageSize: 20, sort: '-amount', search: 'mercado', type: 'Expense' })
  })

  it('envia filtros como query string com os nomes do contrato', async () => {
    mock.onGet('/transactions').reply(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })

    await transactionService.list({
      from: '2026-11-01',
      to: '2026-11-30',
      status: 'Overdue',
      accountId: 'a1',
      categoryId: 'c1',
    })

    expect(mock.history.get[0]?.params).toEqual({
      from: '2026-11-01',
      to: '2026-11-30',
      status: 'Overdue',
      accountId: 'a1',
      categoryId: 'c1',
    })
  })

  it('usa os verbos e rotas de detalhe, ações, histórico e pagamentos', async () => {
    mock.onGet('/transactions/t1').reply(200, { id: 't1', payments: [] })
    mock.onPut('/transactions/t1').reply(200, { id: 't1' })
    mock.onDelete('/transactions/t1').reply(204)
    mock.onPost('/transactions/t1/cancel').reply(200, { id: 't1', status: 'Canceled' })
    mock.onPost('/transactions/t1/reopen').reply(200, { id: 't1', status: 'Pending' })
    mock.onGet('/transactions/t1/history').reply(200, [])
    mock.onPost('/transactions/t1/payments').reply(201, { id: 't1' })
    mock.onDelete('/transactions/t1/payments/p1').reply(200, { id: 't1' })

    await transactionService.get('t1')
    await transactionService.update('t1', {
      description: 'a',
      amount: 1,
      categoryId: 'c',
      transactionDate: '2026-11-18',
    })
    await transactionService.remove('t1')
    expect((await transactionService.cancel('t1')).status).toBe('Canceled')
    expect((await transactionService.reopen('t1')).status).toBe('Pending')
    expect(await transactionService.history('t1')).toEqual([])
    await transactionService.addPayment('t1', { amount: 10, paymentDate: '2026-11-18' })
    await transactionService.removePayment('t1', 'p1')

    expect(mock.history.delete.map((r) => r.url)).toEqual(['/transactions/t1', '/transactions/t1/payments/p1'])
  })
})
