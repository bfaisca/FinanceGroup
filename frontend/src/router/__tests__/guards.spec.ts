import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter, type Router } from 'vue-router'
import { defineComponent } from 'vue'

vi.mock('@/modules/auth/services/authService', () => ({
  authService: { refresh: vi.fn(), login: vi.fn(), logout: vi.fn() },
}))

import { authService } from '@/modules/auth/services/authService'
import { authGuard, pageTitle } from '../guards'
import { routes } from '../routes'

const Stub = defineComponent({ template: '<div />' })

function buildRouter(): Router {
  // Mesma árvore de rotas e metadados da aplicação, com componentes simplificados.
  const stubbed = routes.map((route) => ({
    ...route,
    component: Stub,
    children: route.children?.map((child) => ({ ...child, component: Stub })),
  })) as typeof routes
  const router = createRouter({ history: createMemoryHistory(), routes: stubbed })
  router.beforeEach(authGuard)
  return router
}

const session = {
  accessToken: 'token',
  accessTokenExpiresAt: '2026-11-18T23:00:00Z',
  user: { id: 'u1', name: 'Maria', email: 'maria@exemplo.com', createdAt: '2026-11-18T22:40:00Z' },
}

describe('authGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(authService.refresh).mockReset()
  })

  it('rota protegida sem sessão redireciona para o login com redirect', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()

    await router.push('/accounts?page=2')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/accounts?page=2')
  })

  it('aguarda o bootstrap e permite a rota protegida quando o refresh restaura a sessão', async () => {
    vi.mocked(authService.refresh).mockResolvedValue(session)
    const router = buildRouter()

    await router.push('/settings')

    expect(authService.refresh).toHaveBeenCalledTimes(1)
    expect(router.currentRoute.value.name).toBe('settings')
  })

  it('usuário logado acessando o login vai para o dashboard', async () => {
    vi.mocked(authService.refresh).mockResolvedValue(session)
    const router = buildRouter()

    await router.push('/login')

    expect(router.currentRoute.value.name).toBe('dashboard')
  })

  it('visitante acessa rotas públicas', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()

    await router.push('/register')
    expect(router.currentRoute.value.name).toBe('register')

    await router.push('/reset-password?token=abc')
    expect(router.currentRoute.value.name).toBe('reset-password')
  })

  it('bootstrap executa uma única vez entre navegações', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()

    await router.push('/login')
    await router.push('/register')
    await router.push('/forgot-password')

    expect(authService.refresh).toHaveBeenCalledTimes(1)
  })

  it('todas as 10 rotas do menu exigem autenticação', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()
    const paths = [
      '/',
      '/accounts',
      '/credit-cards',
      '/transactions',
      '/categories',
      '/installments',
      '/recurrences',
      '/groups',
      '/reports',
      '/settings',
    ]
    for (const path of paths) {
      await router.push(path)
      expect(router.currentRoute.value.name, path).toBe('login')
    }
  })

  it('pageTitle usa o título da rota', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()
    await router.push('/login')
    expect(pageTitle(router.currentRoute.value)).toBe('Entrar · GroupFinance')
  })
})
