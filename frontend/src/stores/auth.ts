import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { authService } from '@/modules/auth/services/authService'
import { userService } from '@/modules/settings/services/userService'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useCreditCardsStore } from '@/stores/creditCards'
import type {
  AuthResponse,
  ChangePasswordRequest,
  LoginRequest,
  RegisterRequest,
  UpdateProfileRequest,
  UserDto,
} from '@/types/api'

/**
 * Sessão do usuário (D-005): o access token fica SOMENTE em memória.
 * O refresh token é um cookie httpOnly que o JavaScript não lê.
 */
export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserDto | null>(null)
  const accessToken = ref<string | null>(null)
  const accessTokenExpiresAt = ref<string | null>(null)
  const bootstrapped = ref(false)

  const isAuthenticated = computed(() => accessToken.value !== null && user.value !== null)

  let refreshPromise: Promise<string | null> | null = null
  let bootstrapPromise: Promise<void> | null = null

  function setSession(response: AuthResponse): void {
    accessToken.value = response.accessToken
    accessTokenExpiresAt.value = response.accessTokenExpiresAt
    user.value = response.user
  }

  function clearSession(): void {
    // Dados em cache pertencem ao usuário anterior: nunca devem aparecer para outra sessão.
    useAccountsStore().reset()
    useCategoriesStore().reset()
    useCreditCardsStore().reset()
    accessToken.value = null
    accessTokenExpiresAt.value = null
    user.value = null
  }

  async function login(request: LoginRequest): Promise<UserDto> {
    const response = await authService.login(request)
    setSession(response)
    return response.user
  }

  async function register(request: RegisterRequest): Promise<UserDto> {
    const response = await authService.register(request)
    setSession(response)
    return response.user
  }

  /** Revoga o refresh token no servidor (204 sempre) e limpa a sessão local mesmo se a chamada falhar. */
  async function logout(): Promise<void> {
    try {
      await authService.logout()
    } catch {
      // A sessão local é encerrada de qualquer forma.
    } finally {
      clearSession()
    }
  }

  /**
   * Renova a sessão a partir do cookie. Single-flight: chamadas concorrentes (bootstrap + interceptor)
   * compartilham a mesma requisição, evitando reutilização do refresh token rotacionado.
   * Retorna o novo access token ou `null` (sessão limpa).
   */
  function refresh(): Promise<string | null> {
    if (!refreshPromise) {
      refreshPromise = (async () => {
        try {
          const response = await authService.refresh()
          setSession(response)
          return response.accessToken
        } catch {
          clearSession()
          return null
        }
      })().finally(() => {
        refreshPromise = null
      })
    }
    return refreshPromise
  }

  /** Restaura a sessão na inicialização da aplicação (executa uma única vez). */
  function bootstrap(): Promise<void> {
    if (bootstrapped.value) return Promise.resolve()
    if (!bootstrapPromise) {
      bootstrapPromise = (async () => {
        if (!accessToken.value) {
          await refresh()
        }
        bootstrapped.value = true
      })().finally(() => {
        bootstrapPromise = null
      })
    }
    return bootstrapPromise
  }

  async function fetchCurrentUser(): Promise<UserDto> {
    const current = await userService.getMe()
    user.value = current
    return current
  }

  async function updateProfile(request: UpdateProfileRequest): Promise<UserDto> {
    const updated = await userService.updateMe(request)
    user.value = updated
    return updated
  }

  async function changePassword(request: ChangePasswordRequest): Promise<void> {
    await userService.changePassword(request)
  }

  /** Exclui (anonimiza) a conta; o backend revoga tokens e remove o cookie. */
  async function deleteAccount(password: string): Promise<void> {
    await userService.deleteMe({ password })
    clearSession()
  }

  return {
    user,
    accessToken,
    accessTokenExpiresAt,
    bootstrapped,
    isAuthenticated,
    setSession,
    clearSession,
    login,
    register,
    logout,
    refresh,
    bootstrap,
    fetchCurrentUser,
    updateProfile,
    changePassword,
    deleteAccount,
  }
})
