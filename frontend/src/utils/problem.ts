import { isAxiosError } from 'axios'
import type { ApiErrorCode, ProblemDetails } from '@/types/api'

/** Erro normalizado para exibição na interface. */
export interface AppError {
  message: string
  /** Código estável do contrato, ou `network_error` / `unknown_error` quando não houve resposta válida. */
  code: ApiErrorCode | 'network_error' | 'unknown_error'
  status: number | null
  /** Campo (camelCase, igual ao request) → mensagens. Vazio quando não é `validation_error`. */
  fieldErrors: Record<string, string[]>
}

export const GENERIC_ERROR_MESSAGE = 'Ocorreu um erro inesperado. Tente novamente.'
export const NETWORK_ERROR_MESSAGE = 'Não foi possível conectar ao servidor. Verifique sua conexão e tente novamente.'

const STATUS_MESSAGES: Record<number, string> = {
  400: 'Dados inválidos. Revise as informações e tente novamente.',
  401: 'Sua sessão expirou. Entre novamente.',
  403: 'Você não tem permissão para realizar esta ação.',
  404: 'Recurso não encontrado.',
  409: 'Conflito ao processar a solicitação.',
  422: 'Não foi possível concluir a operação.',
  429: 'Muitas tentativas. Aguarde alguns instantes e tente novamente.',
}

const STATUS_CODES: Record<number, ApiErrorCode> = {
  400: 'validation_error',
  401: 'unauthorized',
  403: 'forbidden',
  404: 'not_found',
  409: 'conflict',
  422: 'business_rule_violation',
  429: 'too_many_requests',
  500: 'internal_error',
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  if (typeof value !== 'object' || value === null) return false
  const v = value as Record<string, unknown>
  return typeof v.title === 'string' || typeof v.detail === 'string' || typeof v.code === 'string'
}

function nonEmpty(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined
}

function normalizeFieldErrors(errors: unknown): Record<string, string[]> {
  if (typeof errors !== 'object' || errors === null) return {}
  const result: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(errors as Record<string, unknown>)) {
    const list = Array.isArray(messages) ? messages : [messages]
    const strings = list.filter((m): m is string => typeof m === 'string' && m.length > 0)
    if (strings.length > 0) result[field] = strings
  }
  return result
}

/** Converte qualquer erro (Axios / ProblemDetails / desconhecido) em `AppError`. */
export function toAppError(error: unknown): AppError {
  if (isAxiosError(error)) {
    const response = error.response
    if (!response) {
      return { message: NETWORK_ERROR_MESSAGE, code: 'network_error', status: null, fieldErrors: {} }
    }

    const status = response.status
    const data: unknown = response.data

    if (isProblemDetails(data)) {
      return {
        message: nonEmpty(data.detail) ?? nonEmpty(data.title) ?? STATUS_MESSAGES[status] ?? GENERIC_ERROR_MESSAGE,
        code: data.code ?? STATUS_CODES[status] ?? (status >= 500 ? 'internal_error' : 'unknown_error'),
        status: data.status ?? status,
        fieldErrors: normalizeFieldErrors(data.errors),
      }
    }

    return {
      message: STATUS_MESSAGES[status] ?? GENERIC_ERROR_MESSAGE,
      code: STATUS_CODES[status] ?? (status >= 500 ? 'internal_error' : 'unknown_error'),
      status,
      fieldErrors: {},
    }
  }

  return { message: GENERIC_ERROR_MESSAGE, code: 'unknown_error', status: null, fieldErrors: {} }
}

/** Primeira mensagem de erro de um campo, se houver. */
export function firstFieldError(fieldErrors: Record<string, string[]>, field: string): string | undefined {
  return fieldErrors[field]?.[0]
}
