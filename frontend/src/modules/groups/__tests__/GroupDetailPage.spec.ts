import { afterEach, beforeEach, describe, expect, it } from 'vitest'
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
})
