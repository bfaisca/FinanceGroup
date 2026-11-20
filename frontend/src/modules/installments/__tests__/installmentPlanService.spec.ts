import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { installmentPlanService, toPlanParams } from '../services/installmentPlanService'

describe('installmentPlanService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('toPlanParams descarta vazios', () => {
    expect(toPlanParams({ page: 2, pageSize: 10, status: undefined, sort: '' })).toEqual({ page: 2, pageSize: 10 })
  })

  it('lista com page, pageSize, status e sort', async () => {
    mock.onGet('/installment-plans').reply(200, { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
    await installmentPlanService.list({ page: 2, pageSize: 10, status: 'Open', sort: '-firstDate' })
    expect(mock.history.get[0]?.params).toEqual({ page: 2, pageSize: 10, status: 'Open', sort: '-firstDate' })
  })

  it('usa as rotas do contrato §12', async () => {
    mock.onGet('/installment-plans/p1').reply(200, { id: 'p1' })
    mock.onPost('/installment-plans').reply(201, { id: 'p1' })
    mock.onPost('/installment-plans/p1/cancel').reply(200, { id: 'p1', status: 'Canceled' })
    mock.onDelete('/installment-plans/p1').reply(204)

    const request = {
      description: 'Notebook',
      totalAmount: 3000,
      installmentCount: 12,
      firstDate: '2026-11-10',
      categoryId: 'c1',
      creditCardId: 'cc1',
      paymentMethod: 'CreditCard' as const,
    }
    await installmentPlanService.get('p1')
    await installmentPlanService.create(request)
    expect((await installmentPlanService.cancel('p1')).status).toBe('Canceled')
    await installmentPlanService.remove('p1')

    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual(request)
    expect(mock.history.delete).toHaveLength(1)
  })
})
