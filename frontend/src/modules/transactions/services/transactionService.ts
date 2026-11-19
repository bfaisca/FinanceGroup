import { http } from '@/services/http'
import type {
  CreatePaymentRequest,
  CreateTransactionRequest,
  PagedResult,
  TransactionDetailDto,
  TransactionDto,
  TransactionHistoryEntryDto,
  TransactionListQuery,
  UpdateTransactionRequest,
  Uuid,
} from '@/types/api'

/** Remove parâmetros vazios/indefinidos da query (o backend aplica os padrões). */
export function toTransactionParams(query: TransactionListQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {}
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null) continue
    if (typeof value === 'string') {
      const trimmed = value.trim()
      if (trimmed.length === 0) continue
      params[key] = trimmed
    } else {
      params[key] = value as number
    }
  }
  return params
}

/** Movimentações — docs/API_CONTRACTS.md §8. */
export const transactionService = {
  async list(query: TransactionListQuery = {}): Promise<PagedResult<TransactionDto>> {
    const { data } = await http.get<PagedResult<TransactionDto>>('/transactions', {
      params: toTransactionParams(query),
    })
    return data
  },

  async get(id: Uuid): Promise<TransactionDetailDto> {
    const { data } = await http.get<TransactionDetailDto>(`/transactions/${id}`)
    return data
  },

  async create(request: CreateTransactionRequest): Promise<TransactionDetailDto> {
    const { data } = await http.post<TransactionDetailDto>('/transactions', request)
    return data
  },

  async update(id: Uuid, request: UpdateTransactionRequest): Promise<TransactionDetailDto> {
    const { data } = await http.put<TransactionDetailDto>(`/transactions/${id}`, request)
    return data
  },

  async remove(id: Uuid): Promise<void> {
    await http.delete(`/transactions/${id}`)
  },

  async cancel(id: Uuid): Promise<TransactionDetailDto> {
    const { data } = await http.post<TransactionDetailDto>(`/transactions/${id}/cancel`)
    return data
  },

  async reopen(id: Uuid): Promise<TransactionDetailDto> {
    const { data } = await http.post<TransactionDetailDto>(`/transactions/${id}/reopen`)
    return data
  },

  async history(id: Uuid): Promise<TransactionHistoryEntryDto[]> {
    const { data } = await http.get<TransactionHistoryEntryDto[]>(`/transactions/${id}/history`)
    return data
  },

  async addPayment(id: Uuid, request: CreatePaymentRequest): Promise<TransactionDetailDto> {
    const { data } = await http.post<TransactionDetailDto>(`/transactions/${id}/payments`, request)
    return data
  },

  async removePayment(id: Uuid, paymentId: Uuid): Promise<TransactionDetailDto> {
    const { data } = await http.delete<TransactionDetailDto>(`/transactions/${id}/payments/${paymentId}`)
    return data
  },
}
