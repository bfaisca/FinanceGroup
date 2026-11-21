import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import { mountWithPlugins } from '@/test/mount'
import NotificationsPage from '../pages/NotificationsPage.vue'
import { useNotificationsStore } from '../store'
import { authedPinia, buttonByText } from './helpers'
import { makeInvitationNotification, makeNotification, makePage } from './fixtures'

describe('NotificationsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/notifications/unread-count').reply(200, { count: 2 })
  })
  afterEach(() => mock.restore())

  async function mountPage() {
    const pinia = authedPinia()
    const store = useNotificationsStore(pinia)
    store.unreadCount = 2
    const mounted = mountWithPlugins(NotificationsPage, { pinia })
    await flushPromises()
    return { ...mounted, store }
  }

  it('lista as notificações com o mesmo item do painel', async () => {
    mock.onGet('/notifications').reply(200, makePage([makeNotification(), makeInvitationNotification()]))
    const { wrapper } = await mountPage()

    expect(wrapper.find('h1').text()).toBe('Notificações')
    expect(wrapper.findAll('li.notif-item')).toHaveLength(2)
    expect(buttonByText(wrapper.element, 'Aceitar')).toBeDefined()
    expect(mock.history.get.find((r) => r.url === '/notifications')?.params).toEqual({ page: 1, pageSize: 20 })
    wrapper.unmount()
  })

  it('estado vazio e filtro vazio têm mensagens distintas', async () => {
    mock.onGet('/notifications').reply(200, makePage([]))
    const { wrapper } = await mountPage()
    expect(wrapper.text()).toContain('Nenhuma notificação')

    await wrapper.find('input[type="checkbox"]').setValue(true)
    await flushPromises()
    expect(wrapper.text()).toContain('Nenhuma notificação não lida')
    wrapper.unmount()
  })

  it('filtro "Somente não lidas" envia unreadOnly e volta à página 1', async () => {
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    const { wrapper } = await mountPage()

    await wrapper.find('input[type="checkbox"]').setValue(true)
    await flushPromises()

    const calls = mock.history.get.filter((r) => r.url === '/notifications')
    expect(calls.at(-1)?.params).toEqual({ page: 1, pageSize: 20, unreadOnly: true })
    expect(wrapper.find('label[for="notif-unread-only"]').text()).toBe('Somente não lidas')
    wrapper.unmount()
  })

  it('"Marcar todas como lidas" atualiza itens e contador', async () => {
    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    mock.onPost('/notifications/read-all').reply(204)
    const { wrapper, store } = await mountPage()

    expect(wrapper.find('[data-testid="unread-marker"]').exists()).toBe(true)
    await buttonByText(wrapper.element, 'Marcar todas como lidas')!.click()
    await flushPromises()

    expect(store.unreadCount).toBe(0)
    expect(wrapper.find('[data-testid="unread-marker"]').exists()).toBe(false)
    expect(buttonByText(wrapper.element, 'Marcar todas como lidas')!.disabled).toBe(true)
    wrapper.unmount()
  })

  it('erro mostra a mensagem e permite tentar novamente', async () => {
    mock.onGet('/notifications').replyOnce(500, { title: 'Falha', detail: 'Falha ao carregar.' })
    const { wrapper } = await mountPage()
    expect(wrapper.text()).toContain('Falha ao carregar.')

    mock.onGet('/notifications').reply(200, makePage([makeNotification()]))
    await buttonByText(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.findAll('li.notif-item')).toHaveLength(1)
    wrapper.unmount()
  })

  it('pagina quando há mais de uma página', async () => {
    mock.onGet('/notifications').reply(200, makePage([makeNotification()], { totalPages: 3, totalCount: 45 }))
    const { wrapper } = await mountPage()
    expect(wrapper.find('.p-paginator, [data-pc-name="paginator"]').exists()).toBe(true)

    const next = wrapper.find('button.p-paginator-next, [data-pc-section="nextbutton"]')
    await next.trigger('click')
    await flushPromises()
    const calls = mock.history.get.filter((r) => r.url === '/notifications')
    expect(calls.at(-1)?.params).toMatchObject({ page: 2 })
    wrapper.unmount()
  })
})
