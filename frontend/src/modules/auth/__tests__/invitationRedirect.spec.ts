import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { mountWithPlugins } from '@/test/mount'
import { isInvitationRedirect } from '@/utils/redirect'
import LoginPage from '../pages/LoginPage.vue'
import RegisterPage from '../pages/RegisterPage.vue'

vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: vi.fn() }) }))

const session = {
  accessToken: 'token',
  accessTokenExpiresAt: '2026-11-20T23:00:00Z',
  user: { id: 'u1', name: 'Ana', email: 'ana@exemplo.com', createdAt: '2026-11-20T10:00:00Z' },
}

async function mountAt(component: typeof LoginPage | typeof RegisterPage, url: string) {
  const Blank = { template: '<div />' }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', name: 'login', component: LoginPage },
      { path: '/register', name: 'register', component: RegisterPage },
      { path: '/forgot-password', name: 'forgot-password', component: Blank },
      { path: '/invitations/:token', name: 'invitation', component: Blank },
      { path: '/', name: 'dashboard', component: Blank },
    ],
  })
  await router.push(url)
  await router.isReady()
  const { wrapper } = mountWithPlugins(component, { pinia: createPinia(), router })
  await flushPromises()
  return { wrapper, router }
}

describe('isInvitationRedirect', () => {
  it('reconhece apenas destinos internos de convite', () => {
    expect(isInvitationRedirect('/invitations/abc')).toBe(true)
    expect(isInvitationRedirect(['/invitations/abc'])).toBe(true)
    expect(isInvitationRedirect('/groups/1')).toBe(false)
    expect(isInvitationRedirect('//evil.com/invitations/x')).toBe(false)
    expect(isInvitationRedirect('https://evil.com/invitations/x')).toBe(false)
    expect(isInvitationRedirect(undefined)).toBe(false)
  })
})

describe('login/cadastro com retorno ao convite', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('login mostra o aviso do convite e preserva o redirect no link de criar conta', async () => {
    const { wrapper } = await mountAt(LoginPage, '/login?redirect=/invitations/tok123')

    expect(wrapper.find('[data-testid="invitation-notice"]').text()).toContain('convite para um grupo')
    const register = wrapper.findAll('a').find((a) => a.text() === 'Criar conta')!
    expect(register.attributes('href')).toBe('/register?redirect=/invitations/tok123')
    wrapper.unmount()
  })

  it('login sem convite não mostra o aviso', async () => {
    const { wrapper } = await mountAt(LoginPage, '/login?redirect=/accounts')
    expect(wrapper.find('[data-testid="invitation-notice"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('após o login volta ao link do convite, com o token', async () => {
    mock.onPost('/auth/login').reply(200, session)
    const { wrapper, router } = await mountAt(LoginPage, '/login?redirect=/invitations/tok123')

    await wrapper.find('input[name="email"]').setValue('ana@exemplo.com')
    await wrapper.find('input[type="password"]').setValue('senha1234')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/invitations/tok123')
    wrapper.unmount()
  })

  it('cadastro mantém o aviso e o redirect no link "Entrar" e volta ao convite', async () => {
    mock.onPost('/auth/register').reply(201, session)
    const { wrapper, router } = await mountAt(RegisterPage, '/register?redirect=/invitations/tok123')

    expect(wrapper.find('[data-testid="invitation-notice"]').exists()).toBe(true)
    const login = wrapper.findAll('a').find((a) => a.text() === 'Entrar')!
    expect(login.attributes('href')).toBe('/login?redirect=/invitations/tok123')

    await wrapper.find('input[name="name"]').setValue('Ana Lima')
    await wrapper.find('input[name="email"]').setValue('ana@exemplo.com')
    const passwords = wrapper.findAll('input[type="password"]')
    for (const input of passwords) await input.setValue('senha1234')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(router.currentRoute.value.fullPath).toBe('/invitations/tok123')
    wrapper.unmount()
  })
})
