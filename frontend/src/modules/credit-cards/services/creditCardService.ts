import { http } from '@/services/http'
import type {
  CardInvoiceDto,
  CreditCardDto,
  CreditCardRequest,
  DateOnlyString,
  PayInvoiceResult,
  Uuid,
} from '@/types/api'

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

  async listInvoices(id: Uuid): Promise<CardInvoiceDto[]> {
    const { data } = await http.get<CardInvoiceDto[]>(`/credit-cards/${id}/invoices`)
    return data
  },

  /** Dá baixa no restante de todas as compras do cartão com o vencimento informado (D-057). */
  async payInvoice(id: Uuid, dueDate: DateOnlyString, paymentDate: DateOnlyString): Promise<PayInvoiceResult> {
    const { data } = await http.post<PayInvoiceResult>(`/credit-cards/${id}/invoices/pay`, { dueDate, paymentDate })
    return data
  },

  async activate(id: Uuid): Promise<CreditCardDto> {
    const { data } = await http.post<CreditCardDto>(`/credit-cards/${id}/activate`)
    return data
  },
}
