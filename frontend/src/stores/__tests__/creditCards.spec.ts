import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { createPinia, setActivePinia } from 'pinia'
import { http } from '@/services/http'
import { makeAccount, makeCreditCard } from '@/test/fixtures'
import { useAccountsStore } from '../accounts'
import { useAuthStore } from '../auth'
import { useCreditCardsStore } from '../creditCards'

describe('store de cartões', () => {
  let mock: MockAdapter
  beforeEach(() => {
    setActivePinia(createPinia())
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('carrega incluindo inativos, uma única vez e em requisição compartilhada', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard(), makeCreditCard({ id: 'cc2', isActive: false })])
    const store = useCreditCardsStore()
    await Promise.all([store.load(), store.load()])
    await store.load()

    expect(mock.history.get).toHaveLength(1)
    expect(mock.history.get[0]?.params).toEqual({ includeInactive: true })
    expect(store.items).toHaveLength(2)
    expect(store.activeItems.map((c) => c.id)).toEqual(['cc1'])
    expect(store.nameOf('cc1')).toBe('Nubank Roxinho')
  })

  it('create/update/(des)ativar recarregam a lista (limite e fatura vêm do backend)', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    mock.onPost('/credit-cards').reply(201, makeCreditCard())
    mock.onPut('/credit-cards/cc1').reply(200, makeCreditCard())
    mock.onPost('/credit-cards/cc1/deactivate').reply(200, makeCreditCard({ isActive: false }))
    mock.onPost('/credit-cards/cc1/activate').reply(200, makeCreditCard())
    const store = useCreditCardsStore()
    const body = { name: 'X', creditLimit: 1, closingDay: 1, dueDay: 2 }

    await store.create(body)
    await store.update('cc1', body)
    await store.deactivate('cc1')
    await store.activate('cc1')

    expect(mock.history.get).toHaveLength(4)
  })

  it('guarda a mensagem de erro de carregamento', async () => {
    mock.onGet('/credit-cards').reply(500, { title: 'Erro interno.' })
    const store = useCreditCardsStore()
    await store.load()
    expect(store.error).toBe('Erro interno.')
    expect(store.loaded).toBe(false)
  })

  it('o encerramento da sessão limpa cartões e demais caches', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    mock.onGet('/accounts').reply(200, [makeAccount()])
    const cards = useCreditCardsStore()
    const accounts = useAccountsStore()
    await Promise.all([cards.load(), accounts.load()])
    expect(cards.items).toHaveLength(1)

    useAuthStore().clearSession()

    expect(cards.items).toEqual([])
    expect(cards.loaded).toBe(false)
    expect(accounts.items).toEqual([])
  })
})
