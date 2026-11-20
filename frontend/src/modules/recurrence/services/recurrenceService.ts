import { http } from '@/services/http'
import type { CreateRecurrenceRequest, RecurrenceDto, UpdateRecurrenceRequest, Uuid } from '@/types/api'

export interface ListRecurrencesOptions {
  includeInactive?: boolean
}

/** Recorrências — docs/API_CONTRACTS.md §13. */
export const recurrenceService = {
  async list(options: ListRecurrencesOptions = {}): Promise<RecurrenceDto[]> {
    const params: Record<string, boolean> = {}
    if (options.includeInactive) params.includeInactive = true
    const { data } = await http.get<RecurrenceDto[]>('/recurrences', { params })
    return data
  },

  async get(id: Uuid): Promise<RecurrenceDto> {
    const { data } = await http.get<RecurrenceDto>(`/recurrences/${id}`)
    return data
  },

  async create(request: CreateRecurrenceRequest): Promise<RecurrenceDto> {
    const { data } = await http.post<RecurrenceDto>('/recurrences', request)
    return data
  },

  async update(id: Uuid, request: UpdateRecurrenceRequest): Promise<RecurrenceDto> {
    const { data } = await http.put<RecurrenceDto>(`/recurrences/${id}`, request)
    return data
  },

  async deactivate(id: Uuid): Promise<RecurrenceDto> {
    const { data } = await http.post<RecurrenceDto>(`/recurrences/${id}/deactivate`)
    return data
  },

  async activate(id: Uuid): Promise<RecurrenceDto> {
    const { data } = await http.post<RecurrenceDto>(`/recurrences/${id}/activate`)
    return data
  },
}
