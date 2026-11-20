import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { reportService } from '../services/reportService'

describe('reportService (§14)', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  const period = { from: '2026-11-01', to: '2026-11-30' }

  it('summary envia from/to e groupBy somente quando informado', async () => {
    mock.onGet('/reports/summary').reply(200, { totals: {}, series: [] })
    await reportService.summary(period)
    await reportService.summary({ ...period, groupBy: 'month' })
    expect(mock.history.get[0]!.params).toEqual(period)
    expect(mock.history.get[1]!.params).toEqual({ ...period, groupBy: 'month' })
  })

  it.each([
    ['byCategory', '/reports/by-category'],
    ['byAccount', '/reports/by-account'],
    ['byPaymentMethod', '/reports/by-payment-method'],
  ] as const)('%s envia o tipo apenas quando informado', async (fn, url) => {
    mock.onGet(url).reply(200, { items: [] })
    await reportService[fn](period)
    await reportService[fn]({ ...period, type: 'Income' })
    expect(mock.history.get[0]!.params).toEqual(period)
    expect(mock.history.get[1]!.params).toEqual({ ...period, type: 'Income' })
  })

  it('monthlyEvolution e installments enviam months; endMonth só se informado', async () => {
    mock.onGet('/reports/monthly-evolution').reply(200, { items: [] })
    mock.onGet('/reports/installments').reply(200, { plans: [], commitmentByMonth: [] })
    await reportService.monthlyEvolution({ months: 6 })
    await reportService.monthlyEvolution({ months: 6, endMonth: '2026-10' })
    await reportService.monthlyEvolution()
    await reportService.installments({ months: 24 })
    expect(mock.history.get[0]!.params).toEqual({ months: 6 })
    expect(mock.history.get[1]!.params).toEqual({ months: 6, endMonth: '2026-10' })
    expect(mock.history.get[2]!.params).toEqual({})
    expect(mock.history.get[3]!.params).toEqual({ months: 24 })
  })

  it('recurrences não envia parâmetros', async () => {
    mock.onGet('/reports/recurrences').reply(200, { activeRules: 0, upcomingByMonth: [], rules: [] })
    await reportService.recurrences()
    expect(mock.history.get[0]!.url).toBe('/reports/recurrences')
  })
})
