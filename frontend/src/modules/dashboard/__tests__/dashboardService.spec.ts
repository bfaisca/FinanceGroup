import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { dashboardService } from '../services/dashboardService'

describe('dashboardService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('envia from/to e devolve o DTO sem transformações', async () => {
    const dto = { today: '2026-11-18', period: { from: '2026-11-01', to: '2026-11-30' } }
    mock.onGet('/dashboard').reply(200, dto)

    const result = await dashboardService.get({ from: '2026-11-01', to: '2026-11-30' })

    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    expect(result).toEqual(dto)
  })
})
