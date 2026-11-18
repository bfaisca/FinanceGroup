import axios, { AxiosHeaders, type AxiosError, type InternalAxiosRequestConfig } from 'axios'

/**
 * Instância Axios única da aplicação (docs/ARCHITECTURE.md §5).
 * - `baseURL` `/api/v1` na mesma origem (D-006); o refresh token trafega em cookie httpOnly (D-005).
 * - Injeta `Authorization: Bearer` a partir da sessão.
 * - Em 401 (exceto rotas `/auth/*`) executa UM refresh compartilhado entre requisições concorrentes
 *   e repete a requisição; se o refresh falhar, encerra a sessão.
 */
export const http = axios.create({
  baseURL: '/api/v1',
  withCredentials: true,
  timeout: 30_000,
  headers: { Accept: 'application/json' },
})

/** Ponte com a camada de sessão (store de auth), configurada em `setupHttpSession`. */
export interface HttpSessionHandlers {
  getAccessToken(): string | null
  /** Tenta renovar a sessão; retorna o novo access token ou `null` se falhar. */
  refreshSession(): Promise<string | null>
  /** Chamado uma única vez por falha de refresh: limpa a sessão e redireciona para o login. */
  onSessionExpired(): void | Promise<void>
}

let handlers: HttpSessionHandlers | null = null
let refreshInFlight: Promise<string | null> | null = null

export function configureHttpSession(sessionHandlers: HttpSessionHandlers | null): void {
  handlers = sessionHandlers
  refreshInFlight = null
}

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean
}

const AUTH_PATH = /^(?:\/api\/v1)?\/auth\//

/** Rotas `/auth/*` nunca disparam refresh automático (login inválido, refresh inválido etc.). */
export function isAuthEndpoint(url: string | undefined): boolean {
  if (!url) return false
  const path = url.replace(/^https?:\/\/[^/]+/i, '')
  return AUTH_PATH.test(path)
}

function bearer(token: string): string {
  return `Bearer ${token}`
}

function setAuthorization(config: InternalAxiosRequestConfig, token: string | null): void {
  const headers = AxiosHeaders.from(config.headers)
  if (token) headers.set('Authorization', bearer(token))
  else headers.delete('Authorization')
  config.headers = headers
}

/** Refresh single-flight: chamadas concorrentes compartilham a mesma promessa. */
function refreshOnce(): Promise<string | null> {
  if (!handlers) return Promise.resolve(null)
  const current = handlers
  if (!refreshInFlight) {
    refreshInFlight = (async () => {
      let token: string | null = null
      try {
        token = await current.refreshSession()
      } catch {
        token = null
      }
      if (!token) {
        await current.onSessionExpired()
      }
      return token
    })().finally(() => {
      refreshInFlight = null
    })
  }
  return refreshInFlight
}

http.interceptors.request.use((config) => {
  if (!isAuthEndpoint(config.url)) {
    const token = handlers?.getAccessToken() ?? null
    if (token) setAuthorization(config, token)
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const config = error.config as RetriableConfig | undefined
    const status = error.response?.status

    if (status !== 401 || !config || config._retried || isAuthEndpoint(config.url) || !handlers) {
      throw error
    }

    config._retried = true

    // Se outra requisição já renovou o token depois que esta foi enviada, apenas repete.
    const sentWith = AxiosHeaders.from(config.headers).get('Authorization')
    const currentToken = handlers.getAccessToken()
    if (currentToken && sentWith !== bearer(currentToken)) {
      setAuthorization(config, currentToken)
      return http.request(config)
    }

    const newToken = await refreshOnce()
    if (!newToken) {
      throw error
    }

    setAuthorization(config, newToken)
    return http.request(config)
  },
)
