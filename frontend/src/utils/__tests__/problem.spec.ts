import { describe, expect, it } from 'vitest'
import { AxiosError, AxiosHeaders, type AxiosResponse, type InternalAxiosRequestConfig } from 'axios'
import { GENERIC_ERROR_MESSAGE, NETWORK_ERROR_MESSAGE, firstFieldError, toAppError } from '../problem'

function axiosError(status: number | null, data?: unknown, code?: string): AxiosError {
  const config = { headers: new AxiosHeaders() } as InternalAxiosRequestConfig
  const response =
    status === null ? undefined : ({ status, statusText: '', headers: {}, config, data } as AxiosResponse)
  return new AxiosError('Request failed', code ?? 'ERR_BAD_REQUEST', config, {}, response)
}

describe('toAppError', () => {
  it('mapeia validation_error com erros por campo', () => {
    const result = toAppError(
      axiosError(400, {
        type: 'https://httpstatuses.io/400',
        title: 'Dados inválidos.',
        status: 400,
        code: 'validation_error',
        traceId: '00-abc',
        errors: { email: ['E-mail inválido.'], password: ['A senha deve ter ao menos 8 caracteres.'] },
      }),
    )
    expect(result).toEqual({
      message: 'Dados inválidos.',
      code: 'validation_error',
      status: 400,
      fieldErrors: { email: ['E-mail inválido.'], password: ['A senha deve ter ao menos 8 caracteres.'] },
    })
    expect(firstFieldError(result.fieldErrors, 'email')).toBe('E-mail inválido.')
    expect(firstFieldError(result.fieldErrors, 'name')).toBeUndefined()
  })

  it('prefere detail a title', () => {
    const result = toAppError(
      axiosError(422, {
        title: 'Regra violada.',
        detail: 'O pagamento excede o valor da movimentação.',
        status: 422,
        code: 'business_rule_violation',
      }),
    )
    expect(result.message).toBe('O pagamento excede o valor da movimentação.')
    expect(result.code).toBe('business_rule_violation')
    expect(result.fieldErrors).toEqual({})
  })

  it('usa title quando detail está vazio', () => {
    const result = toAppError(
      axiosError(401, { title: 'E-mail ou senha inválidos.', detail: '', code: 'invalid_credentials' }),
    )
    expect(result.message).toBe('E-mail ou senha inválidos.')
    expect(result.code).toBe('invalid_credentials')
  })

  it('erro de rede gera mensagem amigável', () => {
    const result = toAppError(axiosError(null, undefined, 'ERR_NETWORK'))
    expect(result).toEqual({ message: NETWORK_ERROR_MESSAGE, code: 'network_error', status: null, fieldErrors: {} })
  })

  it('resposta sem ProblemDetails usa mensagem por status', () => {
    expect(toAppError(axiosError(429, '')).message).toMatch(/Muitas tentativas/)
    expect(toAppError(axiosError(429, '')).code).toBe('too_many_requests')
    const server = toAppError(axiosError(502, '<html>Bad gateway</html>'))
    expect(server.message).toBe(GENERIC_ERROR_MESSAGE)
    expect(server.code).toBe('internal_error')
  })

  it('ignora entradas inválidas em errors', () => {
    const result = toAppError(axiosError(400, { title: 'x', errors: { name: [], email: 'E-mail inválido.', n: [1] } }))
    expect(result.fieldErrors).toEqual({ email: ['E-mail inválido.'] })
  })

  it('erro não-Axios gera mensagem genérica', () => {
    expect(toAppError(new Error('boom'))).toEqual({
      message: GENERIC_ERROR_MESSAGE,
      code: 'unknown_error',
      status: null,
      fieldErrors: {},
    })
    expect(toAppError('x').code).toBe('unknown_error')
  })
})
