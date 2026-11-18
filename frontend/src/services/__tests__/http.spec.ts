import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import type { AxiosRequestConfig } from 'axios'
import { configureHttpSession, http, isAuthEndpoint } from '../http'

function authHeader(config: AxiosRequestConfig): string | undefined {
  const headers = config.headers as Record<string, unknown> | { get?: (k: string) => unknown } | undefined
  if (headers && typeof (headers as { get?: unknown }).get === 'function') {
    return (headers as { get: (k: string) => unknown }).get('Authorization') as string | undefined
  }
  return (headers as Record<string, string> | undefined)?.Authorization
}

describe('http (Axios central)', () => {
  let mock: MockAdapter
  let token: string | null
  let refreshSession: ReturnType<typeof vi.fn<() => Promise<string | null>>>
  let onSessionExpired: ReturnType<typeof vi.fn<() => void>>

  beforeEach(() => {
    mock = new MockAdapter(http)
    token = 'old-token'
    refreshSession = vi.fn(async () => {
      await new Promise((resolve) => setTimeout(resolve, 10))
      token = 'new-token'
      return token
    })
    onSessionExpired = vi.fn(() => {
      token = null
    })
    configureHttpSession({ getAccessToken: () => token, refreshSession, onSessionExpired })
  })

  afterEach(() => {
    mock.restore()
    configureHttpSession(null)
  })

  it('usa baseURL /api/v1 e withCredentials', () => {
    expect(http.defaults.baseURL).toBe('/api/v1')
    expect(http.defaults.withCredentials).toBe(true)
  })

  it('injeta Authorization: Bearer a partir da sessão', async () => {
    mock.onGet('/users/me').reply((config) => [200, { auth: authHeader(config) }])
    const { data } = await http.get('/users/me')
    expect(data.auth).toBe('Bearer old-token')
  })

  it('não injeta Bearer nas rotas /auth/*', async () => {
    mock.onPost('/auth/login').reply((config) => [200, { auth: authHeader(config) ?? null }])
    const { data } = await http.post('/auth/login', {})
    expect(data.auth).toBeNull()
  })

  it('em 401 concorrentes executa UM refresh (single-flight) e repete todas as requisições', async () => {
    const seen: Array<string | undefined> = []
    mock.onGet(/\/items\/\d+/).reply((config) => {
      const auth = authHeader(config)
      seen.push(auth)
      return auth === 'Bearer new-token' ? [200, { url: config.url }] : [401, { code: 'unauthorized' }]
    })

    const results = await Promise.all([http.get('/items/1'), http.get('/items/2'), http.get('/items/3')])

    expect(refreshSession).toHaveBeenCalledTimes(1)
    expect(onSessionExpired).not.toHaveBeenCalled()
    expect(results.map((r) => r.data.url)).toEqual(['/items/1', '/items/2', '/items/3'])
    expect(seen.filter((a) => a === 'Bearer old-token')).toHaveLength(3)
    expect(seen.filter((a) => a === 'Bearer new-token')).toHaveLength(3)
  })

  it('se o token já foi renovado por outra requisição, apenas repete sem novo refresh', async () => {
    mock.onGet('/users/me').replyOnce(() => {
      token = 'rotated-elsewhere'
      return [401, {}]
    })
    mock.onGet('/users/me').reply((config) => [200, { auth: authHeader(config) }])

    const { data } = await http.get('/users/me')
    expect(data.auth).toBe('Bearer rotated-elsewhere')
    expect(refreshSession).not.toHaveBeenCalled()
  })

  it('falha no refresh encerra a sessão uma única vez e rejeita as requisições', async () => {
    refreshSession.mockImplementation(async () => null)
    mock.onGet(/\/items\/\d+/).reply(401, { code: 'unauthorized' })

    const results = await Promise.allSettled([http.get('/items/1'), http.get('/items/2')])

    expect(refreshSession).toHaveBeenCalledTimes(1)
    expect(onSessionExpired).toHaveBeenCalledTimes(1)
    expect(results.every((r) => r.status === 'rejected')).toBe(true)
    expect(token).toBeNull()
  })

  it('exceção no refresh também encerra a sessão', async () => {
    refreshSession.mockRejectedValue(new Error('network'))
    mock.onGet('/users/me').reply(401)
    await expect(http.get('/users/me')).rejects.toMatchObject({ response: { status: 401 } })
    expect(onSessionExpired).toHaveBeenCalledTimes(1)
  })

  it('repete no máximo uma vez: 401 após o refresh é rejeitado', async () => {
    mock.onGet('/users/me').reply(401)
    await expect(http.get('/users/me')).rejects.toMatchObject({ response: { status: 401 } })
    expect(refreshSession).toHaveBeenCalledTimes(1)
  })

  it('401 em rotas /auth/* não dispara refresh', async () => {
    mock.onPost('/auth/login').reply(401, { code: 'invalid_credentials' })
    mock.onPost('/auth/refresh').reply(401, { code: 'invalid_refresh_token' })
    await expect(http.post('/auth/login', {})).rejects.toMatchObject({ response: { status: 401 } })
    await expect(http.post('/auth/refresh')).rejects.toMatchObject({ response: { status: 401 } })
    expect(refreshSession).not.toHaveBeenCalled()
    expect(onSessionExpired).not.toHaveBeenCalled()
  })

  it('erros diferentes de 401 não disparam refresh', async () => {
    mock.onGet('/users/me').reply(403, { code: 'forbidden' })
    await expect(http.get('/users/me')).rejects.toMatchObject({ response: { status: 403 } })
    expect(refreshSession).not.toHaveBeenCalled()
  })

  it('isAuthEndpoint reconhece caminhos relativos e absolutos', () => {
    expect(isAuthEndpoint('/auth/login')).toBe(true)
    expect(isAuthEndpoint('/api/v1/auth/refresh')).toBe(true)
    expect(isAuthEndpoint('http://localhost/api/v1/auth/logout')).toBe(true)
    expect(isAuthEndpoint('/users/me')).toBe(false)
    expect(isAuthEndpoint('/authors')).toBe(false)
    expect(isAuthEndpoint(undefined)).toBe(false)
  })
})
