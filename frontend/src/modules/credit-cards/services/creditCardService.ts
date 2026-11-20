import { http } from '@/services/http'
import type { CreditCardDto, CreditCardRequest, Uuid } from '@/types/api'

export interface ListCreditCardsOptions {
  includeInactive?: boolean
}

/** Cartões de crédito — docs/API_CONTRACTS.md §11. */
export const creditCardService = {
  async list(options: ListCreditCardsOptions = {}): Promise<CreditCardDto[]> {
    const params: Record<string, boolean> = {}
    if (options.includeInactive) params.includeInactive = true
    const { data } = await http.get<CreditCardDto[]>('/credit-cards', { params })
    return data
  },

  async get(id: Uuid): Promise<CreditCardDto> {
    const { data } = await http.get<CreditCardDto>(`/credit-cards/${id}`)
    return data
  },

  async create(request: CreditCardRequest): Promise<CreditCardDto> {
    const { data } = await http.post<CreditCardDto>('/credit-cards', request)
    return data
  },

  async update(id: Uuid, request: CreditCardRequest): Promise<CreditCardDto> {
    const { data } = await http.put<CreditCardDto>(`/credit-cards/${id}`, request)
    return data
  },

  async deactivate(id: Uuid): Promise<CreditCardDto> {
    const { data } = await http.post<CreditCardDto>(`/credit-cards/${id}/deactivate`)
    return data
  },

  async activate(id: Uuid): Promise<CreditCardDto> {
    const { data } = await http.post<CreditCardDto>(`/credit-cards/${id}/activate`)
    return data
  },
}
