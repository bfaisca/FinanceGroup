import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { http } from '@/services/http'
import { useAuthStore } from '@/stores/auth'
import { COUNT_MIN_INTERVAL_MS, useNotificationsStore } from '../store'
import { makeInvitationNotification, makeNotification, makePage } from './fixtures'

function setup(authenticated = true) {
  const pinia = createPinia()
  setActivePinia(pinia)
  if (authenticated) {
    useAuthStore().setSession({
      accessToken: 't',
      accessTokenExpiresAt: '2026-11-21T23:00:00Z',
      user: { id: 'u1', name: 'Maria', email: 'm@x.com', createdAt: '2026-01-01T00:00:00Z' },
    })
  }
  return useNotificationsStore()
}

describe('store de notificações', () => {
  let mock: MockAdapter
  let now = 1_000_000
  beforeEach(() => {
    mock = new MockAdapter(http)
    now = 1_000_000
    vi.spyOn(Date, 'now').mockImplementation(() => now)
  })
  afterEach(() => {
    mock.restore()
    vi.restoreAllMocks()
  })

  describe('contador e throttle', () => {
    it('busca o contador', async () => {
      const store = setup()
      mock.onGet('/notifications/unread-count').reply(200, { count: 4 })
      await store.fetchCount()
      expect(store.unreadCount).toBe(4)
    })

    it('não busca sem sessão', async () => {
      const store = setup(false)
      mock.onGet('/notifications/unread-count').reply(200, { count: 4 })
      await store.fetchCount({ force: true })
      await store.fetchRecent()
      expect(mock.history.get).toHaveLength(0)
      expect(store.unreadCount).toBe(0)
    })

    it('ignora chamadas dentro do intervalo mínimo e libera depois dele', async () => {
      const store = setup()
      mock.onGet('/notifications/unread-count').reply(200, { count: 1 })
      await store.fetchCount()
      await store.fetchCount()
      await store.fetchCount()
      expect(mock.history.get).toHaveLength(1)

      now += COUNT_MIN_INTERVAL_MS + 1
      await store.fetchCount()
      expect(mock.history.get).toHaveLength(2)
    })

    it('`force` ignora o intervalo mas chamadas simultâneas compartilham a requisição', async () => {
      const store = setup()
      mock.onGet('/notifications/unread-count').reply(200, { count: 2 })
      await store.fetchCount()
      await Promise.all([store.fetchCount({ force: true }), store.fetchCount({ force: true })])
      expect(mock.history.get).toHaveLength(2)
    })

    it('falha na busca mantém o contador atual sem lançar erro', async () => {
      const store = setup()
      store.unreadCount = 3
      mock.onGet('/notifications/unread-count').networkError()
      await expect(store.fetchCount()).resolves.toBeUndefined()
      expect(store.unreadCount).toBe(3)
    })
  })

  describe('listas', () => {
    it('carrega recentes (page 1, 8 itens) e a página completa com filtro', async () => {
      const store = setup()
      mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
      await store.fetchRecent()
      await store.fetchPage({ page: 2, pageSize: 20, unreadOnly: true })
      expect(mock.history.get[0]?.params).toEqual({ page: 1, pageSize: 8 })
      expect(mock.history.get[1]?.params).toEqual({ page: 2, pageSize: 20, unreadOnly: true })
      expect(store.recent.items).toHaveLength(1)
      expect(store.full.loaded).toBe(true)
    })

    it('erro na lista guarda a mensagem do backend', async () => {
      const store = setup()
      mock.onGet('/notifications').reply(500, { title: 'Falha', detail: 'Erro interno', code: 'internal_error' })
      await store.fetchRecent()
      expect(store.recent.error).toBe('Erro interno')
      expect(store.recent.loading).toBe(false)
    })
  })

  describe('marcar como lida (otimista)', () => {
    it('diminui o contador e marca o item imediatamente, antes da resposta', async () => {
      const store = setup()
      const n = makeNotification()
      store.unreadCount = 3
      mock.onGet('/notifications').reply(200, makePage([n]))
      await store.fetchRecent()
      let release: () => void = () => undefined
      mock.onPost('/notifications/n1/read').reply(
        () =>
          new Promise((resolve) => {
            release = () => resolve([204])
          }),
      )

      const pending = store.markRead(n)
      expect(store.unreadCount).toBe(2)
      expect(store.recent.items[0]?.isRead).toBe(true)
      await flushPromises()
      release()
      await expect(pending).resolves.toBe(true)
      expect(store.unreadCount).toBe(2)
    })

    it('já lida: não chama a API nem altera o contador', async () => {
      const store = setup()
      store.unreadCount = 3
      await store.markRead(makeNotification({ isRead: true, readAt: '2026-11-21T10:01:00Z' }))
      expect(mock.history.post).toHaveLength(0)
      expect(store.unreadCount).toBe(3)
    })

    it('erro reverte item e contador e reconcilia com o servidor', async () => {
      const store = setup()
      const n = makeNotification()
      store.unreadCount = 3
      mock.onGet('/notifications').reply(200, makePage([n]))
      await store.fetchRecent()
      mock.onPost('/notifications/n1/read').reply(500)
      mock.onGet('/notifications/unread-count').reply(200, { count: 3 })

      await expect(store.markRead(n)).resolves.toBe(false)
      await flushPromises()

      expect(store.recent.items[0]?.isRead).toBe(false)
      expect(store.unreadCount).toBe(3)
      expect(mock.history.get.some((r) => r.url === '/notifications/unread-count')).toBe(true)
    })

    it('marcar todas zera o contador e reverte em erro', async () => {
      const store = setup()
      store.unreadCount = 5
      mock.onGet('/notifications').reply(200, makePage([makeNotification(), makeNotification({ id: 'n2' })]))
      await store.fetchRecent()
      mock.onPost('/notifications/read-all').replyOnce(204)
      await expect(store.markAllRead()).resolves.toBe(true)
      expect(store.unreadCount).toBe(0)
      expect(store.recent.items.every((i) => i.isRead)).toBe(true)

      store.unreadCount = 5
      store.recent.items = [makeNotification()]
      mock.onPost('/notifications/read-all').replyOnce(500)
      mock.onGet('/notifications/unread-count').reply(200, { count: 5 })
      await expect(store.markAllRead()).resolves.toBe(false)
      expect(store.unreadCount).toBe(5)
      expect(store.recent.items[0]?.isRead).toBe(false)
    })

    it('a resposta atrasada do contador não sobrescreve uma ação local posterior', async () => {
      const store = setup()
      let release: () => void = () => undefined
      mock.onGet('/notifications/unread-count').reply(
        () =>
          new Promise((resolve) => {
            release = () => resolve([200, { count: 9 }])
          }),
      )
      mock.onPost('/notifications/read-all').reply(204)
      const fetching = store.fetchCount()
      await store.markAllRead()
      release()
      await fetching
      expect(store.unreadCount).toBe(0)
    })
  })

  describe('convites', () => {
    it('aceitar: marca como lida, atualiza o status e devolve o grupo', async () => {
      const store = setup()
      const n = makeInvitationNotification()
      store.unreadCount = 2
      mock.onGet('/notifications').reply(200, makePage([n]))
      await store.fetchRecent()
      mock.onPost('/notifications/n-inv/accept-invitation').reply(200, { id: 'g1', name: 'Casa' })

      const group = await store.acceptInvitation(n)

      expect(group).toMatchObject({ id: 'g1' })
      expect(store.unreadCount).toBe(1)
      expect(store.recent.items[0]).toMatchObject({
        isRead: true,
        invitation: { status: 'Accepted', canRespond: false },
      })
    })

    it('recusar atualiza o status para Declined', async () => {
      const store = setup()
      const n = makeInvitationNotification()
      store.unreadCount = 1
      mock.onGet('/notifications').reply(200, makePage([n]))
      await store.fetchRecent()
      mock.onPost('/notifications/n-inv/decline-invitation').reply(204)
      await store.declineInvitation(n)
      expect(store.unreadCount).toBe(0)
      expect(store.recent.items[0]?.invitation?.status).toBe('Declined')
    })

    it('422 propaga o erro, mantém contador e recarrega a lista', async () => {
      const store = setup()
      const n = makeInvitationNotification()
      store.unreadCount = 1
      mock.onGet('/notifications').reply(200, makePage([n]))
      mock.onGet('/notifications/unread-count').reply(200, { count: 1 })
      await store.fetchRecent()
      mock.onPost('/notifications/n-inv/accept-invitation').reply(422, { title: 'x', detail: 'Convite expirado.' })

      await expect(store.acceptInvitation(n)).rejects.toMatchObject({ response: { status: 422 } })
      await flushPromises()

      expect(store.unreadCount).toBe(1)
      expect(mock.history.get.filter((r) => r.url === '/notifications')).toHaveLength(2)
    })
  })

  it('reset (logout) limpa tudo e descarta respostas em andamento', async () => {
    const store = setup()
    store.unreadCount = 4
    store.recent.items = [makeNotification()]
    let release: () => void = () => undefined
    mock.onGet('/notifications/unread-count').reply(
      () =>
        new Promise((resolve) => {
          release = () => resolve([200, { count: 9 }])
        }),
    )
    const fetching = store.fetchCount({ force: true })
    await flushPromises()
    useAuthStore().clearSession()
    release()
    await fetching

    expect(store.unreadCount).toBe(0)
    expect(store.recent.items).toEqual([])
    expect(store.full.loaded).toBe(false)
  })
})
