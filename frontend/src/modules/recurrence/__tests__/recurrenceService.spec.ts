import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { recurrenceService } from '../services/recurrenceService'

describe('recurrenceService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista com includeInactive apenas quando solicitado', async () => {
    mock.onGet('/recurrences').reply(200, [])
    await recurrenceService.list()
    await recurrenceService.list({ includeInactive: true })
    expect(mock.history.get[0]?.params).toEqual({})
    expect(mock.history.get[1]?.params).toEqual({ includeInactive: true })
  })

  it('usa as rotas do contrato §13', async () => {
    mock.onGet('/recurrences/r1').reply(200, { id: 'r1' })
    mock.onPost('/recurrences').reply(201, { id: 'r1' })
    mock.onPut('/recurrences/r1').reply(200, { id: 'r1' })
    mock.onPost('/recurrences/r1/deactivate').reply(200, { id: 'r1', isActive: false })
    mock.onPost('/recurrences/r1/activate').reply(200, { id: 'r1', isActive: true })

    const create = {
      description: 'Netflix',
      type: 'Expense' as const,
      amount: 55.9,
      categoryId: 'c1',
      frequency: 'Monthly' as const,
      interval: 1,
      startDate: '2026-11-10',
    }
    await recurrenceService.get('r1')
    await recurrenceService.create(create)
    await recurrenceService.update('r1', {
      description: 'Netflix',
      amount: 60,
      categoryId: 'c1',
      frequency: 'Monthly',
      interval: 1,
    })
    expect((await recurrenceService.deactivate('r1')).isActive).toBe(false)
    expect((await recurrenceService.activate('r1')).isActive).toBe(true)
    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual(create)
    expect('type' in JSON.parse(mock.history.put[0]?.data as string)).toBe(false)
  })
})
