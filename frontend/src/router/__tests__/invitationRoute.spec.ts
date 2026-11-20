import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { defineComponent } from 'vue'

vi.mock('@/modules/auth/services/authService', () => ({
  authService: { refresh: vi.fn(), login: vi.fn(), logout: vi.fn() },
}))

import { authService } from '@/modules/auth/services/authService'
import { authGuard } from '../guards'
import { routes } from '../routes'

const Stub = defineComponent({ template: '<div />' })

function buildRouter() {
  const stubbed = routes.map((route) => ({
    ...route,
    component: Stub,
    children: route.children?.map((child) => ({ ...child, component: Stub })),
  })) as typeof routes
  const router = createRouter({ history: createMemoryHistory(), routes: stubbed })
  router.beforeEach(authGuard)
  return router
}

describe('rotas de grupos', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(authService.refresh).mockReset()
  })

  it('/invitations/:token sem sessão vai ao login preservando o token no redirect', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()

    await router.push('/invitations/abc_DEF-123')

    expect(router.currentRoute.value.name).toBe('login')
    expect(router.currentRoute.value.query.redirect).toBe('/invitations/abc_DEF-123')
  })

  it('com sessão, /invitations/:token e /groups/:id são acessíveis', async () => {
    vi.mocked(authService.refresh).mockResolvedValue({
      accessToken: 't',
      accessTokenExpiresAt: '2026-11-20T23:00:00Z',
      user: { id: 'u1', name: 'Ana', email: 'ana@exemplo.com', createdAt: '2026-11-20T10:00:00Z' },
    })
    const router = buildRouter()

    await router.push('/invitations/tok')
    expect(router.currentRoute.value.name).toBe('invitation')
    expect(router.currentRoute.value.params.token).toBe('tok')

    await router.push('/groups/g1?tab=members')
    expect(router.currentRoute.value.name).toBe('group-detail')
    expect(router.currentRoute.value.params.id).toBe('g1')
  })

  it('/groups/:id sem sessão exige login', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const router = buildRouter()
    await router.push('/groups/g1')
    expect(router.currentRoute.value.name).toBe('login')
  })
})
