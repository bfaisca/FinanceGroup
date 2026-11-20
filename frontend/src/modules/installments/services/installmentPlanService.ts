import { http } from '@/services/http'
import type {
  CreateInstallmentPlanRequest,
  InstallmentPlanDetailDto,
  InstallmentPlanDto,
  InstallmentPlanListQuery,
  PagedResult,
  Uuid,
} from '@/types/api'

/** Remove parâmetros vazios (o backend aplica os padrões). */
export function toPlanParams(query: InstallmentPlanListQuery): Record<string, string | number> {
  const params: Record<string, string | number> = {}
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === '') continue
    params[key] = value as string | number
  }
  return params
}

/** Parcelamentos — docs/API_CONTRACTS.md §12. */
export const installmentPlanService = {
  async list(query: InstallmentPlanListQuery = {}): Promise<PagedResult<InstallmentPlanDto>> {
    const { data } = await http.get<PagedResult<InstallmentPlanDto>>('/installment-plans', {
      params: toPlanParams(query),
    })
    return data
  },

  async get(id: Uuid): Promise<InstallmentPlanDetailDto> {
    const { data } = await http.get<InstallmentPlanDetailDto>(`/installment-plans/${id}`)
    return data
  },

  async create(request: CreateInstallmentPlanRequest): Promise<InstallmentPlanDetailDto> {
    const { data } = await http.post<InstallmentPlanDetailDto>('/installment-plans', request)
    return data
  },

  async cancel(id: Uuid): Promise<InstallmentPlanDetailDto> {
    const { data } = await http.post<InstallmentPlanDetailDto>(`/installment-plans/${id}/cancel`)
    return data
  },

  async remove(id: Uuid): Promise<void> {
    await http.delete(`/installment-plans/${id}`)
  },
}
