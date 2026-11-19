import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { createPinia, setActivePinia } from 'pinia'
import { http } from '@/services/http'
import { useAccountsStore } from '../accounts'
import { useCategoriesStore } from '../categories'
import { useClockStore } from '../clock'
import type { AccountDto, CategoryDto } from '@/types/api'

function category(partial: Partial<CategoryDto>): CategoryDto {
  return {
    id: 'c1',
    name: 'Moradia',
    type: 'Expense',
    parentCategoryId: null,
    icon: null,
    color: null,
    isDefault: true,
    isActive: true,
    ...partial,
  }
}

function account(partial: Partial<AccountDto>): AccountDto {
  return {
    id: 'a1',
    name: 'Nubank',
    institution: null,
    type: 'Checking',
    currency: 'BRL',
    isActive: true,
    latestSnapshot: null,
    projectedBalance: null,
    projectedAsOf: '2026-11-18',
    createdAt: '2026-11-18T10:00:00Z',
    updatedAt: '2026-11-18T10:00:00Z',
    ...partial,
  }
}

describe('stores compartilhados', () => {
  let mock: MockAdapter
  beforeEach(() => {
    setActivePinia(createPinia())
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('categorias: carrega incluindo inativas, uma única vez e em requisição compartilhada', async () => {
    mock.onGet('/categories').reply(200, [category({}), category({ id: 'c2', isActive: false })])
    const store = useCategoriesStore()

    await Promise.all([store.load(), store.load()])
    await store.load()

    expect(mock.history.get).toHaveLength(1)
    expect(mock.history.get[0]?.params).toEqual({ includeInactive: true })
    expect(store.items).toHaveLength(2)
    expect(store.activeItems).toHaveLength(1)
    expect(store.nameOf('c1')).toBe('Moradia')
  })

  it('categorias: desativar recarrega a lista (subcategorias também mudam)', async () => {
    mock
      .onGet('/categories')
      .replyOnce(200, [category({ id: 'p', isDefault: false }), category({ id: 'f', parentCategoryId: 'p' })])
    mock.onPost('/categories/p/deactivate').reply(200, category({ id: 'p', isActive: false }))
    mock
      .onGet('/categories')
      .reply(200, [
        category({ id: 'p', isDefault: false, isActive: false }),
        category({ id: 'f', parentCategoryId: 'p', isActive: false }),
      ])
    const store = useCategoriesStore()
    await store.load()

    await store.deactivate('p')

    expect(store.items.every((c) => !c.isActive)).toBe(true)
  })

  it('categorias: guarda a mensagem de erro e permite tentar novamente', async () => {
    mock.onGet('/categories').replyOnce(500, { title: 'Erro interno.' })
    const store = useCategoriesStore()
    await store.load()
    expect(store.error).toBe('Erro interno.')
    expect(store.loaded).toBe(false)

    mock.onGet('/categories').reply(200, [category({})])
    await store.load()
    expect(store.error).toBeNull()
    expect(store.items).toHaveLength(1)
  })

  it('contas: carrega e atualiza o "hoje" da aplicação a partir de projectedAsOf', async () => {
    mock.onGet('/accounts').reply(200, [account({ projectedAsOf: '2026-11-19' })])
    const store = useAccountsStore()
    const clock = useClockStore()

    await store.load()

    expect(mock.history.get[0]?.params).toEqual({ includeInactive: true })
    expect(clock.today).toBe('2026-11-19')
    expect(clock.fromServer).toBe(true)
    expect(store.nameOf('a1')).toBe('Nubank')
  })

  it('contas: reset limpa o cache (troca de usuário)', async () => {
    mock.onGet('/accounts').reply(200, [account({})])
    const store = useAccountsStore()
    await store.load()
    store.reset()
    expect(store.items).toEqual([])
    expect(store.loaded).toBe(false)
  })

  it('relógio ignora datas inválidas', () => {
    const clock = useClockStore()
    const before = clock.today
    clock.setServerToday('18/11/2026')
    expect(clock.today).toBe(before)
  })
})
