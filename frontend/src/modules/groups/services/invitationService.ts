import { http } from '@/services/http'
import type { GroupSummaryDto, InvitationPreviewDto } from '../types'

/** Convites recebidos (pelo token do link) — docs/API_CONTRACTS.md §16.1. */
export const invitationService = {
  async preview(token: string): Promise<InvitationPreviewDto> {
    const { data } = await http.get<InvitationPreviewDto>(`/invitations/${encodeURIComponent(token)}`)
    return data
  },

  async accept(token: string): Promise<GroupSummaryDto> {
    const { data } = await http.post<GroupSummaryDto>(`/invitations/${encodeURIComponent(token)}/accept`)
    return data
  },

  async decline(token: string): Promise<void> {
    await http.post(`/invitations/${encodeURIComponent(token)}/decline`)
  },
}
