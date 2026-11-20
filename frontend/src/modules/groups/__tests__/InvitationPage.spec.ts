import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { useAuthStore } from '@/stores/auth'
import InvitationPage from '../pages/InvitationPage.vue'
import { makeGroupSummary, makePreview } from './fixtures'
import { button, mountAt } from './helpers'

async function mountPage(preview: Parameters<typeof makePreview>[0] = {}, status = 200) {
  const pinia = createPinia()
  useAuthStore(pinia).user = { id: 'u3', name: 'Ana Lima', email: 'ana@exemplo.com', createdAt: '2026-01-01T00:00:00Z' }
  const mock = currentMock
  if (status === 200) mock.onGet('/invitations/tok123').reply(200, makePreview(preview))
  else mock.onGet('/invitations/tok123').reply(status, { title: 'Convite não encontrado.', code: 'not_found' })
  return mountAt(InvitationPage, '/invitations/tok123', { path: '/invitations/:token', name: 'invitation' }, { pinia })
}

let currentMock: MockAdapter

describe('InvitationPage', () => {
  beforeEach(() => {
    currentMock = new MockAdapter(http)
  })
  afterEach(() => currentMock.restore())

  it('mostra o preview (grupo, quem convidou, e-mail) e oferece aceitar/recusar', async () => {
    const { wrapper } = await mountPage()

    expect(wrapper.find('[data-testid="invitation-group"]').text()).toBe('Casa')
    expect(wrapper.find('[data-testid="invitation-by"]').text()).toBe('Maria Silva')
    expect(wrapper.find('[data-testid="invitation-email"]').text()).toBe('ana@exemplo.com')
    expect(button(wrapper.element, 'Aceitar convite')).toBeDefined()
    expect(button(wrapper.element, 'Recusar')).toBeDefined()
    wrapper.unmount()
  })

  it('aceitar chama /accept, avisa e abre o grupo', async () => {
    const { wrapper, router } = await mountPage()
    currentMock.onPost('/invitations/tok123/accept').reply(200, makeGroupSummary({ id: 'g9', name: 'Casa' }))

    await button(wrapper.element, 'Aceitar convite')!.click()
    await flushPromises()

    expect(currentMock.history.post[0]?.url).toBe('/invitations/tok123/accept')
    expect(router.currentRoute.value.fullPath).toBe('/groups/g9')
    wrapper.unmount()
  })

  it('recusar pede confirmação, chama /decline e volta à lista de grupos', async () => {
    const { wrapper, router } = await mountPage()
    currentMock.onPost('/invitations/tok123/decline').reply(204)

    await button(wrapper.element, 'Recusar')!.click()
    await flushPromises()
    expect(currentMock.history.post).toHaveLength(0)

    document.body.querySelector('.confirm-dialog')!.dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(currentMock.history.post[0]?.url).toBe('/invitations/tok123/decline')
    expect(router.currentRoute.value.name).toBe('groups')
    wrapper.unmount()
  })

  it('falha ao aceitar (422) mostra a explicação do backend e recarrega o convite', async () => {
    const { wrapper } = await mountPage()
    currentMock.onPost('/invitations/tok123/accept').reply(422, {
      title: 'Regra violada',
      detail: 'Este convite expirou.',
      code: 'business_rule_violation',
    })

    await button(wrapper.element, 'Aceitar convite')!.click()
    await flushPromises()

    expect(wrapper.find('[data-testid="invitation-error"]').text()).toBe('Este convite expirou.')
    expect(currentMock.history.get.filter((r) => r.url === '/invitations/tok123')).toHaveLength(2)
    wrapper.unmount()
  })

  it('e-mail diferente: explica, mostra os dois e-mails e NÃO oferece aceitar', async () => {
    const { wrapper } = await mountPage({ email: 'outra@exemplo.com', canAccept: false })

    const note = wrapper.find('[data-testid="invitation-wrong-email"]').text()
    expect(note).toContain('outra@exemplo.com')
    expect(note).toContain('ana@exemplo.com')
    expect(button(wrapper.element, 'Aceitar convite')).toBeUndefined()
    expect(button(wrapper.element, 'Entrar com outra conta')).toBeDefined()
    wrapper.unmount()
  })

  it('"Entrar com outra conta" encerra a sessão e volta ao login mantendo o token', async () => {
    const { wrapper, router } = await mountPage({ email: 'outra@exemplo.com', canAccept: false })
    currentMock.onPost('/auth/logout').reply(204)

    await button(wrapper.element, 'Entrar com outra conta')!.click()
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/invitations/tok123')
    wrapper.unmount()
  })

  it.each([
    ['Expired', 'expirou'],
    ['Revoked', 'revogado'],
    ['Accepted', 'já foi aceito'],
    ['Declined', 'foi recusado'],
  ] as const)('convite %s mostra o estado e nenhuma ação de aceite', async (status, text) => {
    const { wrapper } = await mountPage({ status, canAccept: false })

    expect(wrapper.find('[data-testid="invitation-state"]').text()).toContain(text)
    expect(button(wrapper.element, 'Aceitar convite')).toBeUndefined()
    expect(button(wrapper.element, 'Ir para meus grupos')).toBeDefined()
    wrapper.unmount()
  })

  it('já é membro: informa e não oferece aceitar', async () => {
    const { wrapper } = await mountPage({ alreadyMember: true, canAccept: false })

    expect(wrapper.find('[data-testid="invitation-state"]').text()).toContain('já participa')
    expect(button(wrapper.element, 'Aceitar convite')).toBeUndefined()
    wrapper.unmount()
  })

  it('token inexistente (404) mostra "Convite não encontrado"', async () => {
    const { wrapper } = await mountPage({}, 404)
    expect(wrapper.find('[data-testid="invitation-not-found"]').text()).toContain('Convite não encontrado')
    wrapper.unmount()
  })

  it('erro de rede/servidor permite tentar novamente', async () => {
    const pinia = createPinia()
    currentMock.onGet('/invitations/tok123').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountAt(
      InvitationPage,
      '/invitations/tok123',
      { path: '/invitations/:token', name: 'invitation' },
      { pinia },
    )
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    currentMock.onGet('/invitations/tok123').reply(200, makePreview())
    await button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.find('[data-testid="invitation-group"]').exists()).toBe(true)
    wrapper.unmount()
  })
})
