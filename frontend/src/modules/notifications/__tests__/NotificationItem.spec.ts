import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { defineComponent } from 'vue'
import Toast from 'primevue/toast'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import { mountWithPlugins } from '@/test/mount'
import NotificationItem from '../components/NotificationItem.vue'
import { useNotificationsStore } from '../store'
import { authedPinia, buttonByText } from './helpers'
import { makeInvitationNotification, makeNotification } from './fixtures'

describe('NotificationItem', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  function mountItem(notification = makeNotification()) {
    const pinia = authedPinia()
    const store = useNotificationsStore(pinia)
    store.recent.items = [notification]
    store.unreadCount = 1
    // O host renderiza o Toast (como o App.vue) e passa sempre o item atual do store.
    const Host = defineComponent({
      components: { NotificationItem, Toast },
      setup: () => ({ store }),
      template: '<div><Toast /><ul><NotificationItem :notification="store.recent.items[0]" /></ul></div>',
    })
    const mounted = mountWithPlugins(Host, { pinia })
    return { ...mounted, store, item: () => mounted.wrapper.findComponent(NotificationItem) }
  }

  it('mostra o texto, o tempo relativo com data completa no title e o indicador de não lida (texto, não só cor)', () => {
    const { wrapper } = mountItem()
    expect(wrapper.text().replace(/ /g, ' ')).toContain('Brendo lançou uma nova despesa em Casa: Mercado (R$ 300,00)')
    expect(wrapper.find('[data-testid="unread-marker"]').text()).toBe('Nova')
    expect(wrapper.find('.sr-only').text()).toContain('Não lida')
    const time = wrapper.find('time')
    expect(time.attributes('title')).toMatch(/\d{2}\/\d{2}\/\d{4}/)
    expect(time.attributes('datetime')).toBe('2026-11-21T10:00:00Z')
    wrapper.unmount()
  })

  it('lida: sem indicador de não lida', () => {
    const { wrapper } = mountItem(makeNotification({ isRead: true, readAt: '2026-11-21T10:05:00Z' }))
    expect(wrapper.find('[data-testid="unread-marker"]').exists()).toBe(false)
    expect(wrapper.find('.sr-only').exists()).toBe(false)
    wrapper.unmount()
  })

  it('clicar abre a movimentação no grupo (?tab=transactions&open=) e marca como lida', async () => {
    mock.onPost('/notifications/n1/read').reply(204)
    const { wrapper, router, store, item } = mountItem()
    await router.push('/dashboard')

    await wrapper.find('button.notif-item__main').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/groups/g1?tab=transactions&open=t1')
    expect(mock.history.post[0]?.url).toBe('/notifications/n1/read')
    expect(store.unreadCount).toBe(0)
    expect(item().emitted('navigate')).toHaveLength(1)
    wrapper.unmount()
  })

  it('o corpo é um botão focável (teclado) para movimentações', () => {
    const { wrapper } = mountItem()
    const main = wrapper.find('.notif-item__main')
    expect(main.element.tagName).toBe('BUTTON')
    wrapper.unmount()
  })

  describe('convite', () => {
    it('com canRespond mostra Aceitar e Recusar (e o corpo não é um link)', () => {
      const { wrapper } = mountItem(makeInvitationNotification())
      expect(wrapper.text()).toContain('Ana convidou você para o grupo Casa')
      expect(wrapper.find('.notif-item__main').element.tagName).toBe('DIV')
      expect(buttonByText(wrapper.element, 'Aceitar')).toBeDefined()
      expect(buttonByText(wrapper.element, 'Recusar')).toBeDefined()
      wrapper.unmount()
    })

    it('sem canRespond não mostra os botões', () => {
      const { wrapper } = mountItem(makeInvitationNotification({}, { canRespond: false }))
      expect(buttonByText(wrapper.element, 'Aceitar')).toBeUndefined()
      expect(buttonByText(wrapper.element, 'Recusar')).toBeUndefined()
      expect(wrapper.find('[data-testid="invitation-status"]').text()).toBe('Convite indisponível')
      wrapper.unmount()
    })

    it.each([
      ['Accepted', 'Convite aceito'],
      ['Declined', 'Convite recusado'],
      ['Expired', 'Convite expirado'],
      ['Revoked', 'Convite cancelado'],
    ] as const)('status %s mostra o rótulo "%s" sem ações', (status, label) => {
      const { wrapper } = mountItem(makeInvitationNotification({}, { status, canRespond: false }))
      expect(wrapper.find('[data-testid="invitation-status"]').text()).toBe(label)
      expect(buttonByText(wrapper.element, 'Aceitar')).toBeUndefined()
      wrapper.unmount()
    })

    it('Aceitar: chama a API, avisa e navega ao grupo', async () => {
      mock.onPost('/notifications/n-inv/accept-invitation').reply(200, { id: 'g7', name: 'Casa' })
      const { wrapper, router, store } = mountItem(makeInvitationNotification())

      await buttonByText(wrapper.element, 'Aceitar')!.click()
      await flushPromises()

      expect(router.currentRoute.value.fullPath).toBe('/groups/g7')
      expect(document.body.textContent).toContain('Convite aceito')
      expect(store.unreadCount).toBe(0)
      wrapper.unmount()
    })

    it('Recusar: chama a API, avisa e atualiza o rótulo', async () => {
      mock.onPost('/notifications/n-inv/decline-invitation').reply(204)
      const { wrapper } = mountItem(makeInvitationNotification())

      await buttonByText(wrapper.element, 'Recusar')!.click()
      await flushPromises()

      expect(mock.history.post[0]?.url).toBe('/notifications/n-inv/decline-invitation')
      expect(document.body.textContent).toContain('Convite recusado')
      expect(wrapper.find('[data-testid="invitation-status"]').text()).toBe('Convite recusado')
      wrapper.unmount()
    })

    it('422 vira toast com o detail e não navega', async () => {
      mock
        .onPost('/notifications/n-inv/accept-invitation')
        .reply(422, { title: 'Regra', detail: 'Este convite expirou.' })
      mock.onGet().reply(200, { count: 1, items: [], page: 1, pageSize: 8, totalCount: 0, totalPages: 0 })
      const { wrapper, router } = mountItem(makeInvitationNotification())
      await router.push('/dashboard')

      await buttonByText(wrapper.element, 'Aceitar')!.click()
      await flushPromises()

      expect(document.body.textContent).toContain('Este convite expirou.')
      expect(router.currentRoute.value.fullPath).toBe('/dashboard')
      wrapper.unmount()
    })

    it('403 vira toast com o detail', async () => {
      mock
        .onPost('/notifications/n-inv/accept-invitation')
        .reply(403, { title: 'Proibido', detail: 'E-mail da conta diferente.' })
      mock.onGet().reply(200, { count: 1 })
      const { wrapper } = mountItem(makeInvitationNotification())
      await buttonByText(wrapper.element, 'Aceitar')!.click()
      await flushPromises()
      expect(document.body.textContent).toContain('E-mail da conta diferente.')
      wrapper.unmount()
    })

    it('convite não respondível e não lido oferece "Marcar como lida"', async () => {
      mock.onPost('/notifications/n-inv/read').reply(204)
      const { wrapper, store } = mountItem(makeInvitationNotification({}, { status: 'Expired', canRespond: false }))
      await buttonByText(wrapper.element, 'Marcar como lida')!.click()
      await flushPromises()
      expect(mock.history.post[0]?.url).toBe('/notifications/n-inv/read')
      expect(store.unreadCount).toBe(0)
      wrapper.unmount()
    })
  })
})
