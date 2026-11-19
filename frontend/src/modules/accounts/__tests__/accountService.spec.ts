import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { accountService } from '../services/accountService'

describe('accountService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista com includeInactive e asOf somente quando informados', async () => {
    mock.onGet('/accounts').reply(200, [])
    await accountService.list()
    await accountService.list({ includeInactive: true, asOf: '2026-12-01' })

    expect(mock.history.get[0]?.params).toEqual({})
    expect(mock.history.get[1]?.params).toEqual({ includeInactive: true, asOf: '2026-12-01' })
  })

  it('cria conta com saldo inicial e devolve o DTO', async () => {
    const dto = { id: 'a1', name: 'Nubank' }
    mock.onPost('/accounts').reply(201, dto)

    const result = await accountService.create({
      name: 'Nubank',
      type: 'Checking',
      initialBalance: 100.5,
      balanceReferenceDate: '2026-11-18',
    })

    expect(result).toEqual(dto)
    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual({
      name: 'Nubank',
      type: 'Checking',
      initialBalance: 100.5,
      balanceReferenceDate: '2026-11-18',
    })
  })

  it('usa as rotas de ativar/desativar e snapshots do contrato', async () => {
    mock.onPost('/accounts/a1/deactivate').reply(200, { id: 'a1', isActive: false })
    mock.onPost('/accounts/a1/activate').reply(200, { id: 'a1', isActive: true })
    mock.onGet('/accounts/a1/balance-snapshots').reply(200, [{ id: 's1' }])
    mock.onPost('/accounts/a1/balance-snapshots').reply(201, { id: 's2' })
    mock.onPut('/accounts/a1').reply(200, { id: 'a1' })
    mock.onGet('/accounts/a1').reply(200, { id: 'a1' })

    expect((await accountService.deactivate('a1')).isActive).toBe(false)
    expect((await accountService.activate('a1')).isActive).toBe(true)
    expect(await accountService.listSnapshots('a1')).toHaveLength(1)
    expect((await accountService.createSnapshot('a1', { balance: 10, referenceDate: '2026-11-18' })).id).toBe('s2')
    await accountService.update('a1', { name: 'X', type: 'Savings' })
    await accountService.get('a1', '2026-12-31')
    expect(mock.history.get.at(-1)?.params).toEqual({ asOf: '2026-12-31' })
  })
})
