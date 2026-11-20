import { http } from '@/services/http'
import type { PagedResult, Uuid } from '@/types/api'
import type {
  CreateGroupPaymentRequest,
  CreateGroupTransactionRequest,
  GroupTransactionDetailDto,
  GroupTransactionDto,
  GroupTransactionHistoryEntryDto,
  GroupTransactionListQuery,
  UpdateGroupTransactionRequest,
} from '../types'

/** Remove parâmetros vazios/indefinidos da query (o backend aplica os padrões). */
export function toGroupTransactionParams(query: GroupTransactionListQuery): Record<string, string | number> {
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

/** Movimentações do grupo — docs/API_CONTRACTS.md §16.2. */
export const groupTransactionService = {
  async list(groupId: Uuid, query: GroupTransactionListQuery = {}): Promise<PagedResult<GroupTransactionDto>> {
    const { data } = await http.get<PagedResult<GroupTransactionDto>>(`/groups/${groupId}/transactions`, {
      params: toGroupTransactionParams(query),
    })
    return data
  },

  async get(groupId: Uuid, id: Uuid): Promise<GroupTransactionDetailDto> {
    const { data } = await http.get<GroupTransactionDetailDto>(`/groups/${groupId}/transactions/${id}`)
    return data
  },

  async create(groupId: Uuid, request: CreateGroupTransactionRequest): Promise<GroupTransactionDetailDto> {
    const { data } = await http.post<GroupTransactionDetailDto>(`/groups/${groupId}/transactions`, request)
    return data
  },

  async update(groupId: Uuid, id: Uuid, request: UpdateGroupTransactionRequest): Promise<GroupTransactionDetailDto> {
    const { data } = await http.put<GroupTransactionDetailDto>(`/groups/${groupId}/transactions/${id}`, request)
    return data
  },

  async remove(groupId: Uuid, id: Uuid): Promise<void> {
    await http.delete(`/groups/${groupId}/transactions/${id}`)
  },

  async cancel(groupId: Uuid, id: Uuid): Promise<GroupTransactionDetailDto> {
    const { data } = await http.post<GroupTransactionDetailDto>(`/groups/${groupId}/transactions/${id}/cancel`)
    return data
  },

  async reopen(groupId: Uuid, id: Uuid): Promise<GroupTransactionDetailDto> {
    const { data } = await http.post<GroupTransactionDetailDto>(`/groups/${groupId}/transactions/${id}/reopen`)
    return data
  },

  async history(groupId: Uuid, id: Uuid): Promise<GroupTransactionHistoryEntryDto[]> {
    const { data } = await http.get<GroupTransactionHistoryEntryDto[]>(`/groups/${groupId}/transactions/${id}/history`)
    return data
  },

  async addPayment(groupId: Uuid, id: Uuid, request: CreateGroupPaymentRequest): Promise<GroupTransactionDetailDto> {
    const { data } = await http.post<GroupTransactionDetailDto>(
      `/groups/${groupId}/transactions/${id}/payments`,
      request,
    )
    return data
  },

  async removePayment(groupId: Uuid, id: Uuid, paymentId: Uuid): Promise<GroupTransactionDetailDto> {
    const { data } = await http.delete<GroupTransactionDetailDto>(
      `/groups/${groupId}/transactions/${id}/payments/${paymentId}`,
    )
    return data
  },
}
