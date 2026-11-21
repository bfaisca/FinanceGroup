import { http } from '@/services/http'
import type { PagedResult, Uuid } from '@/types/api'
import type { GroupSummaryDto } from '@/modules/groups/types'
import type { NotificationDto, NotificationListQuery, UnreadCountDto } from '../types'

/** Notificações do usuário autenticado — docs/API_CONTRACTS.md §17. */
export const notificationService = {
  async list(query: NotificationListQuery = {}): Promise<PagedResult<NotificationDto>> {
    const params: Record<string, number | boolean> = {}
    if (query.page !== undefined) params.page = query.page
    if (query.pageSize !== undefined) params.pageSize = query.pageSize
    if (query.unreadOnly) params.unreadOnly = true
    const { data } = await http.get<PagedResult<NotificationDto>>('/notifications', { params })
    return data
  },

  async unreadCount(): Promise<number> {
    const { data } = await http.get<UnreadCountDto>('/notifications/unread-count')
    return data.count
  },

  async markRead(id: Uuid): Promise<void> {
    await http.post(`/notifications/${id}/read`)
  },

  async markAllRead(): Promise<void> {
    await http.post('/notifications/read-all')
  },

  async acceptInvitation(id: Uuid): Promise<GroupSummaryDto> {
    const { data } = await http.post<GroupSummaryDto>(`/notifications/${id}/accept-invitation`)
    return data
  },

  async declineInvitation(id: Uuid): Promise<void> {
    await http.post(`/notifications/${id}/decline-invitation`)
  },
}
