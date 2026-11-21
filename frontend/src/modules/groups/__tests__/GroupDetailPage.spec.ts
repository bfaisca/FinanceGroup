import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { createPinia } from 'pinia'
import ToastEventBus from 'primevue/toasteventbus'
import { useAuthStore } from '@/stores/auth'
import { useNotificationsStore } from '@/modules/notifications/store'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupDetailPage from '../pages/GroupDetailPage.vue'
import { makeGroupDashboard, makeGroupDetail, makeMemberViewDetail } from './fixtures'
import { button, mountAt } from './helpers'

const mountPage = (url = '/groups/g1') => mountAt(GroupDetailPage, url, { path: '/groups/:id', name: 'group-detail' })

function tabLabels(wrapper: { findAll: (s: string) => { text: () => string }[] }): string[] {
  return wrapper.findAll('[role="tab"]').map((t) => t.text())
}

describe('GroupDetailPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    mock.onGet('/categories').reply(200, [])
    mock.onGet('/accounts').reply(200, [])
  })
  afterEach(() => mock.restore())

  it('Owner/Admin veem as cinco abas, inclusive Convites', async () => {
    mock.onGet('/groups/g1').reply(200, makeGroupDetail())
    const { wrapper } = await mountPage()

    expect(tabLabels(wrapper)).toEqual(['Resumo', 'Movimentações', 'Membros', 'Convites', 'Configurações'])
    expect(wrapper.find('h1').text()).toBe('Casa')
    expect(wrapper.text()).toContain('Proprietário')
    wrapper.unmount()
  })

  it('Member comum NÃO vê a aba Convites e, se forçar `?tab=invitations`, cai no Resumo', async () => {
    mock.onGet('/groups/g1').reply(200, makeMemberViewDetail())
    const { wrapper } = await mountPage('/groups/g1?tab=invitations')

    expect(tabLabels(wrapper)).toEqual(['Resumo', 'Movimentações', 'Membros', 'Configurações'])
    expect(wrapper.find('[role="tab"][aria-selected="true"]').text()).toBe('Resumo')
    wrapper.unmount()
  })

  it('a aba ativa vem da URL e trocar de aba atualiza `?tab=` descartando parâmetros da anterior', async () => {
    mock.onGet('/groups/g1').reply(200, makeGroupDetail())
    mock.onGet('/groups/g1/transactions').reply(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
    mock.onGet('/groups/g1/members').reply(200, [])
    const { wrapper, router } = await mountPage('/groups/g1?from=2026-10-01&to=2026-10-31')

    const tab = wrapper.findAll('[role="tab"]').find((t) => t.text() === 'Movimentações')!
    await tab.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ tab: 'transactions' })
    expect(wrapper.find('[role="tab"][aria-selected="true"]').text()).toBe('Movimentações')
    wrapper.unmount()
  })

  it('grupo arquivado mostra selo e aviso de somente leitura', async () => {
    mock.onGet('/groups/g1').reply(200, makeGroupDetail({ isActive: false }))
    const { wrapper } = await mountPage()

    expect(wrapper.find('[data-testid="archived-banner"]').text()).toContain('somente leitura')
    expect(wrapper.text()).toContain('Arquivado')
    wrapper.unmount()
  })

  it('404 mostra "não encontrado ou sem participação" e volta aos grupos', async () => {
    mock.onGet('/groups/g1').reply(404, { title: 'Recurso não encontrado.', code: 'not_found' })
    const { wrapper, router } = await mountPage()

    expect(wrapper.find('[data-testid="group-not-found"]').text()).toContain('não participa mais')
    await button(wrapper.element, 'Voltar aos grupos')!.click()
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('groups')
    wrapper.unmount()
  })

  it('erro genérico permite tentar novamente', async () => {
    mock.onGet('/groups/g1').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountPage()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/groups/g1').reply(200, makeGroupDetail())
    await button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.find('h1').text()).toBe('Casa')
    wrapper.unmount()
  })

  it('Member sai do grupo pela aba Configurações, com confirmação, e volta à lista', async () => {
    mock.onGet('/groups/g1').reply(200, makeMemberViewDetail())
    mock.onPost('/groups/g1/leave').reply(204)
    const { wrapper, router } = await mountPage('/groups/g1?tab=settings')

    await button(wrapper.element, 'Sair do grupo')!.click()
    await flushPromises()
    expect(mock.history.post).toHaveLength(0)
    expect(document.body.textContent).toContain('deixará de ver o grupo')

    await button(document.body, 'Sair do grupo')!.click()
    // Há dois botões com o mesmo texto (página e diálogo): confirma pelo submit do diálogo.
    document.body.querySelector('.confirm-dialog')!.dispatchEvent(new Event('submit'))
    await flushPromises()

    expect(mock.history.post.some((r) => r.url === '/groups/g1/leave')).toBe(true)
    expect(router.currentRoute.value.name).toBe('groups')
    wrapper.unmount()
  })

  describe('botão Atualizar (D-050)', () => {
    let toasts: { severity: string; summary: string }[] = []
    const collect = (message: { severity: string; summary: string }) => toasts.push(message)
    beforeEach(() => {
      toasts = []
      ToastEventBus.on('add', collect)
    })
    afterEach(() => ToastEventBus.off('add', collect))

    const refreshButton = (wrapper: { find: (s: string) => { element: Element } }) =>
      wrapper.find('[data-testid="refresh-button"]').element as HTMLButtonElement

    it('aparece em todas as abas, com nome acessível', async () => {
      mock.onGet('/groups/g1').reply(200, makeGroupDetail())
      mock
        .onGet('/groups/g1/transactions')
        .reply(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
      mock.onGet('/groups/g1/members').reply(200, [])
      for (const tab of ['', '?tab=transactions', '?tab=members', '?tab=settings']) {
        const { wrapper } = await mountPage(`/groups/g1${tab}`)
        const el = refreshButton(wrapper)
        expect(el.getAttribute('aria-label')).toBe('Atualizar dados do grupo')
        wrapper.unmount()
      }
    })

    it('recarrega o grupo, o dashboard da aba atual e o contador de notificações; mostra "Atualizado às"', async () => {
      mock.onGet('/groups/g1').reply(200, makeGroupDetail())
      mock.onGet('/notifications/unread-count').reply(200, { count: 3 })
      const pinia = createPinia()
      useAuthStore(pinia).setSession({
        accessToken: 't',
        accessTokenExpiresAt: '2026-11-21T23:00:00Z',
        user: { id: 'u1', name: 'Maria', email: 'm@x.com', createdAt: '2026-01-01T00:00:00Z' },
      })
      const { wrapper } = await mountAt(
        GroupDetailPage,
        '/groups/g1',
        { path: '/groups/:id', name: 'group-detail' },
        { pinia },
      )

      const count = (url: string) => mock.history.get.filter((r) => r.url === url).length
      expect(count('/groups/g1')).toBe(1)
      expect(count('/groups/g1/dashboard')).toBe(1)

      refreshButton(wrapper).click()
      await flushPromises()

      expect(count('/groups/g1')).toBe(2)
      expect(count('/groups/g1/dashboard')).toBe(2)
      expect(count('/notifications/unread-count')).toBe(1)
      expect(useNotificationsStore(pinia).unreadCount).toBe(3)
      expect(wrapper.find('[data-testid="last-updated"]').text()).toMatch(/^Atualizado às \d{2}:\d{2}$/)
      expect(toasts).toEqual([expect.objectContaining({ severity: 'success', summary: 'Dados atualizados' })])
      wrapper.unmount()
    })

    it('na aba de movimentações recarrega a lista e os membros', async () => {
      mock.onGet('/groups/g1').reply(200, makeGroupDetail())
      mock
        .onGet('/groups/g1/transactions')
        .reply(200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
      mock.onGet('/groups/g1/members').reply(200, [])
      mock.onGet('/categories').reply(200, [])
      const { wrapper } = await mountPage('/groups/g1?tab=transactions')
      const count = (url: string) => mock.history.get.filter((r) => r.url === url).length
      expect(count('/groups/g1/transactions')).toBe(1)

      refreshButton(wrapper).click()
      await flushPromises()
      expect(count('/groups/g1/transactions')).toBe(2)
      expect(count('/groups/g1/members')).toBe(2)
      // O dashboard não está montado nesta aba: carrega fresco ao voltar ao Resumo.
      expect(count('/groups/g1/dashboard')).toBe(0)
      wrapper.unmount()
    })

    it('sem rajada: durante a atualização o botão fica em "atualizando" e cliques extras são ignorados', async () => {
      mock.onGet('/groups/g1').reply(200, makeGroupDetail())
      const { wrapper } = await mountPage()
      let release!: () => void
      mock
        .onGet('/groups/g1/dashboard')
        .reply(() => new Promise((resolve) => (release = () => resolve([200, makeGroupDashboard()]))))

      const el = refreshButton(wrapper)
      el.click()
      await flushPromises()
      expect(el.getAttribute('aria-busy')).toBe('true')
      expect(wrapper.find('[data-testid="last-updated"]').text()).toBe('Atualizando…')
      el.click()
      el.click()
      await flushPromises()
      expect(mock.history.get.filter((r) => r.url === '/groups/g1')).toHaveLength(2)

      release()
      await flushPromises()
      expect(el.getAttribute('aria-busy')).toBe('false')
      expect(wrapper.find('[data-testid="last-updated"]').text()).toContain('Atualizado às')
      wrapper.unmount()
    })

    it('falha: avisa que não foi possível atualizar e mantém os dados anteriores', async () => {
      mock.onGet('/groups/g1').reply(200, makeGroupDetail())
      const { wrapper } = await mountPage()
      mock.onGet('/groups/g1/dashboard').reply(500, { title: 'Erro interno.' })

      refreshButton(wrapper).click()
      await flushPromises()

      expect(toasts).toEqual([expect.objectContaining({ severity: 'error', summary: 'Não foi possível atualizar' })])
      expect(wrapper.find('h1').text()).toBe('Casa')
      wrapper.unmount()
    })
  })
})
