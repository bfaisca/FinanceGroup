import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import { mountWithPlugins } from '@/test/mount'
import NotificationBell from '../components/NotificationBell.vue'
import { useNotificationsStore } from '../store'
import { authedPinia, buttonByText } from './helpers'
import { makeInvitationNotification, makeNotification, makePage } from './fixtures'

describe('NotificationBell', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  function mountBell(count = 3) {
    const pinia = authedPinia()
    const store = useNotificationsStore(pinia)
    store.unreadCount = count
    mock.onGet('/notifications/unread-count').reply(200, { count })
    const mounted = mountWithPlugins(NotificationBell, { pinia })
    return { ...mounted, store }
  }

  const trigger = (w: ReturnType<typeof mountBell>['wrapper']) => w.find('[data-testid="notification-bell"]')

  it('sem não lidas: sem badge e com aria-label sem contagem', () => {
    const { wrapper } = mountBell(0)
    expect(wrapper.find('[data-testid="notification-badge"]').exists()).toBe(false)
    expect(trigger(wrapper).attributes('aria-label')).toBe('Notificações, nenhuma não lida')
    wrapper.unmount()
  })

  it('badge com a contagem e aria-label acessível (singular/plural)', async () => {
    const { wrapper, store } = mountBell(3)
    expect(wrapper.find('[data-testid="notification-badge"]').text()).toBe('3')
    expect(trigger(wrapper).attributes('aria-label')).toBe('Notificações, 3 não lidas')
    store.unreadCount = 1
    await flushPromises()
    expect(trigger(wrapper).attributes('aria-label')).toBe('Notificações, 1 não lida')
    wrapper.unmount()
  })

  it('limita o badge a 99+', () => {
    const { wrapper } = mountBell(150)
    expect(wrapper.find('[data-testid="notification-badge"]').text()).toBe('99+')
    expect(trigger(wrapper).attributes('aria-label')).toBe('Notificações, 150 não lidas')
    wrapper.unmount()
  })

  it('abrir recarrega contador e lista, mostra os itens e foca o painel', async () => {
    const { wrapper } = mountBell()
    mock.onGet('/notifications').reply(200, makePage([makeNotification(), makeInvitationNotification()]))

    expect(trigger(wrapper).attributes('aria-expanded')).toBe('false')
    await trigger(wrapper).trigger('click')
    await flushPromises()

    expect(trigger(wrapper).attributes('aria-expanded')).toBe('true')
    const panel = wrapper.find('[data-testid="notification-panel"]')
    expect(panel.attributes('role')).toBe('dialog')
    expect(panel.findAll('li')).toHaveLength(2)
    expect(document.activeElement).toBe(panel.element)
    expect(mock.history.get.map((r) => r.url)).toEqual(
      expect.arrayContaining(['/notifications', '/notifications/unread-count']),
    )
    expect(mock.history.get.find((r) => r.url === '/notifications')?.params).toEqual({ page: 1, pageSize: 8 })
    expect(panel.text()).toContain('Ver todas')
    wrapper.unmount()
  })

  it('estado vazio', async () => {
    const { wrapper } = mountBell()
    mock.onGet('/notifications').reply(200, makePage([]))
    await trigger(wrapper).trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="notification-empty"]').text()).toBe('Você não tem notificações.')
    wrapper.unmount()
  })

  it('estado de carregamento e de erro com nova tentativa', async () => {
    const { wrapper } = mountBell()
    mock.onGet('/notifications').replyOnce(500, { title: 'Falha', detail: 'Serviço indisponível.' })
    await trigger(wrapper).trigger('click')
    expect(wrapper.find('[data-testid="notification-panel"]').text()).toContain('Carregando notificações')
    await flushPromises()

    const panel = wrapper.find('[data-testid="notification-panel"]')
    expect(panel.text()).toContain('Serviço indisponível.')
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    await buttonByText(panel.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(panel.findAll('li')).toHaveLength(1)
    wrapper.unmount()
  })

  it('"Marcar todas como lidas" zera o contador; fica desabilitado sem não lidas', async () => {
    const { wrapper, store } = mountBell(2)
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    mock.onPost('/notifications/read-all').reply(204)
    await trigger(wrapper).trigger('click')
    await flushPromises()

    const markAll = () => buttonByText(wrapper.element, 'Marcar todas como lidas')!
    expect(markAll().disabled).toBe(false)
    await markAll().click()
    await flushPromises()

    expect(store.unreadCount).toBe(0)
    expect(wrapper.find('[data-testid="notification-badge"]').exists()).toBe(false)
    expect(markAll().disabled).toBe(true)
    wrapper.unmount()
  })

  it('"Ver todas" leva a /notifications e fecha o painel', async () => {
    const { wrapper, router } = mountBell()
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    await trigger(wrapper).trigger('click')
    await flushPromises()

    await wrapper.find('a.notif-panel__all').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('notifications')
    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('Escape fecha o painel e devolve o foco ao sino (teclado)', async () => {
    const { wrapper } = mountBell()
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    await trigger(wrapper).trigger('click')
    await flushPromises()

    await wrapper.find('[data-testid="notification-panel"]').trigger('keydown', { key: 'Escape' })
    await flushPromises()

    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(false)
    expect(document.activeElement).toBe(trigger(wrapper).element)
    wrapper.unmount()
  })

  it('clique fora fecha; clique no sino alterna', async () => {
    const { wrapper } = mountBell()
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    await trigger(wrapper).trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(true)

    document.body.dispatchEvent(new Event('pointerdown', { bubbles: true }))
    await flushPromises()
    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(false)

    await trigger(wrapper).trigger('click')
    await trigger(wrapper).trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('clicar em uma movimentação navega, marca como lida e fecha o painel', async () => {
    const { wrapper, router, store } = mountBell(1)
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    mock.onPost('/notifications/n1/read').reply(204)
    await trigger(wrapper).trigger('click')
    await flushPromises()

    await wrapper.find('button.notif-item__main').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/groups/g1?tab=transactions&open=t1')
    expect(store.unreadCount).toBe(0)
    expect(wrapper.find('[data-testid="notification-panel"]').exists()).toBe(false)
    wrapper.unmount()
  })
})
