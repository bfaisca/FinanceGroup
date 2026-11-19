import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { http } from '@/services/http'
import { categoryService } from '../services/categoryService'

describe('categoryService', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('lista (plana) com filtros opcionais', async () => {
    mock.onGet('/categories').reply(200, [])
    await categoryService.list()
    await categoryService.list({ type: 'Income', includeInactive: true })
    expect(mock.history.get[0]?.params).toEqual({})
    expect(mock.history.get[1]?.params).toEqual({ type: 'Income', includeInactive: true })
  })

  it('cria, edita e alterna ativação pelas rotas do contrato', async () => {
    mock.onPost('/categories').reply(201, { id: 'c1' })
    mock.onPut('/categories/c1').reply(200, { id: 'c1' })
    mock.onPost('/categories/c1/deactivate').reply(200, { id: 'c1', isActive: false })
    mock.onPost('/categories/c1/activate').reply(200, { id: 'c1', isActive: true })

    await categoryService.create({
      name: 'Pets',
      type: 'Expense',
      parentCategoryId: null,
      icon: 'pi-heart',
      color: '#dc2626',
    })
    await categoryService.update('c1', { name: 'Pets 2', icon: null, color: null })
    expect((await categoryService.deactivate('c1')).isActive).toBe(false)
    expect((await categoryService.activate('c1')).isActive).toBe(true)
    expect(JSON.parse(mock.history.put[0]?.data as string)).toEqual({ name: 'Pets 2', icon: null, color: null })
  })
})
