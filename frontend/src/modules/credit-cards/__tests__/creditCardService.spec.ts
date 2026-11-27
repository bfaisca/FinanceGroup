import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { creditCardService } from '../services/creditCardService'

describe('creditCardService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista com includeInactive apenas quando solicitado', async () => {
    mock.onGet('/credit-cards').reply(200, [])
    await creditCardService.list()
    await creditCardService.list({ includeInactive: true })
    expect(mock.history.get[0]?.params).toEqual({})
    expect(mock.history.get[1]?.params).toEqual({ includeInactive: true })
  })

  it('usa as rotas do contrato §11', async () => {
    mock.onGet('/credit-cards/c1').reply(200, { id: 'c1' })
    mock.onPost('/credit-cards').reply(201, { id: 'c1' })
    mock.onPut('/credit-cards/c1').reply(200, { id: 'c1' })
    mock.onPost('/credit-cards/c1/deactivate').reply(200, { id: 'c1', isActive: false })
    mock.onPost('/credit-cards/c1/activate').reply(200, { id: 'c1', isActive: true })

    const body = { name: 'Roxinho', institution: null, creditLimit: 5000, closingDay: 5, dueDay: 12, accountId: null }
    await creditCardService.get('c1')
    await creditCardService.create(body)
    await creditCardService.update('c1', body)
    expect((await creditCardService.deactivate('c1')).isActive).toBe(false)
    expect((await creditCardService.activate('c1')).isActive).toBe(true)
    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual(body)
    expect(JSON.parse(mock.history.put[0]?.data as string)).toEqual(body)
  })

  it('lista faturas em aberto e paga uma fatura (D-057)', async () => {
    mock.onGet('/credit-cards/c1/invoices').reply(200, [{ dueDate: '2026-12-12', remainingAmount: 10 }])
    mock.onPost('/credit-cards/c1/invoices/pay').reply(200, { paidAmount: 10, purchasesPaid: 1 })

    expect(await creditCardService.listInvoices('c1')).toHaveLength(1)
    await creditCardService.payInvoice('c1', '2026-12-12', '2026-11-27')
    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual({
      dueDate: '2026-12-12',
      paymentDate: '2026-11-27',
    })
  })
})
