import { http } from '@/services/http'
import type { DashboardDto, DashboardQuery } from '@/types/api'

/** Dashboard individual — docs/API_CONTRACTS.md §9. */
export const dashboardService = {
  async get(query: DashboardQuery): Promise<DashboardDto> {
    const { data } = await http.get<DashboardDto>('/dashboard', { params: { from: query.from, to: query.to } })
    return data
  },
}
