import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { AuthResponse } from '@/types/api'

vi.mock('@/modules/auth/services/authService', () => ({
  authService: {
    login: vi.fn(),
    register: vi.fn(),
    refresh: vi.fn(),
    logout: vi.fn(),
    forgotPassword: vi.fn(),
    resetPassword: vi.fn(),
  },
}))

vi.mock('@/modules/settings/services/userService', () => ({
  userService: {
    getMe: vi.fn(),
    updateMe: vi.fn(),
    changePassword: vi.fn(),
    deleteMe: vi.fn(),
  },
}))

import { authService } from '@/modules/auth/services/authService'
import { userService } from '@/modules/settings/services/userService'
import { useAuthStore } from '../auth'

const authResponse = (token = 'access-1'): AuthResponse => ({
  accessToken: token,
  accessTokenExpiresAt: '2026-11-18T23:00:00Z',
  user: {
    id: '6f1c2d4e-0000-4000-8000-000000000001',
    name: 'Maria Silva',
    email: 'maria@exemplo.com',
    createdAt: '2026-11-18T22:40:00Z',
  },
})

describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(authService.login).mockReset()
    vi.mocked(authService.register).mockReset()
    vi.mocked(authService.refresh).mockReset()
    vi.mocked(authService.logout).mockReset()
    vi.mocked(userService.updateMe).mockReset()
    vi.mocked(userService.deleteMe).mockReset()
  })

  it('inicia sem sessão', () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)
    expect(auth.accessToken).toBeNull()
    expect(auth.user).toBeNull()
  })

  it('login guarda usuário e access token em memória', async () => {
    vi.mocked(authService.login).mockResolvedValue(authResponse())
    const auth = useAuthStore()

    await auth.login({ email: 'maria@exemplo.com', password: 'Senha123' })

    expect(authService.login).toHaveBeenCalledWith({ email: 'maria@exemplo.com', password: 'Senha123' })
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.accessToken).toBe('access-1')
    expect(auth.user?.name).toBe('Maria Silva')
    expect(window.localStorage.length).toBe(0)
    expect(window.sessionStorage.length).toBe(0)
  })

  it('login com erro não cria sessão', async () => {
    vi.mocked(authService.login).mockRejectedValue(new Error('invalid'))
    const auth = useAuthStore()
    await expect(auth.login({ email: 'a@b.com', password: 'x' })).rejects.toThrow()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('register cria sessão', async () => {
    vi.mocked(authService.register).mockResolvedValue(authResponse())
    const auth = useAuthStore()
    await auth.register({ name: 'Maria Silva', email: 'maria@exemplo.com', password: 'Senha123' })
    expect(auth.isAuthenticated).toBe(true)
  })

  it('logout chama o servidor e limpa a sessão', async () => {
    vi.mocked(authService.login).mockResolvedValue(authResponse())
    vi.mocked(authService.logout).mockResolvedValue()
    const auth = useAuthStore()
    await auth.login({ email: 'maria@exemplo.com', password: 'Senha123' })

    await auth.logout()

    expect(authService.logout).toHaveBeenCalledTimes(1)
    expect(auth.isAuthenticated).toBe(false)
    expect(auth.accessToken).toBeNull()
  })

  it('logout limpa a sessão mesmo se o servidor falhar', async () => {
    vi.mocked(authService.login).mockResolvedValue(authResponse())
    vi.mocked(authService.logout).mockRejectedValue(new Error('network'))
    const auth = useAuthStore()
    await auth.login({ email: 'maria@exemplo.com', password: 'Senha123' })

    await expect(auth.logout()).resolves.toBeUndefined()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('bootstrap restaura a sessão via refresh (cookie)', async () => {
    vi.mocked(authService.refresh).mockResolvedValue(authResponse('restored'))
    const auth = useAuthStore()

    await auth.bootstrap()

    expect(authService.refresh).toHaveBeenCalledTimes(1)
    expect(auth.bootstrapped).toBe(true)
    expect(auth.isAuthenticated).toBe(true)
    expect(auth.accessToken).toBe('restored')
  })

  it('bootstrap sem cookie válido termina sem sessão', async () => {
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const auth = useAuthStore()

    await auth.bootstrap()

    expect(auth.bootstrapped).toBe(true)
    expect(auth.isAuthenticated).toBe(false)
  })

  it('bootstrap executa apenas uma vez, mesmo concorrente', async () => {
    vi.mocked(authService.refresh).mockResolvedValue(authResponse())
    const auth = useAuthStore()

    await Promise.all([auth.bootstrap(), auth.bootstrap(), auth.bootstrap()])
    await auth.bootstrap()

    expect(authService.refresh).toHaveBeenCalledTimes(1)
  })

  it('refresh é single-flight e falha limpa a sessão', async () => {
    vi.mocked(authService.login).mockResolvedValue(authResponse())
    vi.mocked(authService.refresh).mockRejectedValue(new Error('401'))
    const auth = useAuthStore()
    await auth.login({ email: 'maria@exemplo.com', password: 'Senha123' })

    const [a, b] = await Promise.all([auth.refresh(), auth.refresh()])

    expect(a).toBeNull()
    expect(b).toBeNull()
    expect(authService.refresh).toHaveBeenCalledTimes(1)
    expect(auth.isAuthenticated).toBe(false)
  })

  it('updateProfile atualiza o usuário', async () => {
    vi.mocked(authService.login).mockResolvedValue(authResponse())
    const auth = useAuthStore()
    await auth.login({ email: 'maria@exemplo.com', password: 'Senha123' })
    vi.mocked(userService.updateMe).mockResolvedValue({ ...authResponse().user, name: 'Maria S. Silva' })

    await auth.updateProfile({ name: 'Maria S. Silva' })

    expect(userService.updateMe).toHaveBeenCalledWith({ name: 'Maria S. Silva' })
    expect(auth.user?.name).toBe('Maria S. Silva')
  })

  it('deleteAccount envia a senha e encerra a sessão', async () => {
    vi.mocked(authService.login).mockResolvedValue(authResponse())
    vi.mocked(userService.deleteMe).mockResolvedValue()
    const auth = useAuthStore()
    await auth.login({ email: 'maria@exemplo.com', password: 'Senha123' })

    await auth.deleteAccount('Senha123')

    expect(userService.deleteMe).toHaveBeenCalledWith({ password: 'Senha123' })
    expect(auth.isAuthenticated).toBe(false)
  })
})
