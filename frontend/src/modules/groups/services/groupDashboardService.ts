import { http } from '@/services/http'
import type { Uuid } from '@/types/api'
import type { GroupDashboardDto, GroupDashboardQuery } from '../types'

/** Dashboard do grupo — docs/API_CONTRACTS.md §16.3. */
export const groupDashboardService = {
  async get(groupId: Uuid, query: GroupDashboardQuery): Promise<GroupDashboardDto> {
    const params: Record<string, string | number> = { from: query.from, to: query.to }
    if (query.months !== undefined) params.months = query.months
    if (query.endMonth) params.endMonth = query.endMonth
    const { data } = await http.get<GroupDashboardDto>(`/groups/${groupId}/dashboard`, { params })
    return data
  },
}
