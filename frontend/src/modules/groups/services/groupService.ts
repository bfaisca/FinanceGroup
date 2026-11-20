import { http } from '@/services/http'
import type { GroupInvitationStatus, Uuid } from '@/types/api'
import type {
  AssignableRole,
  GroupDetailDto,
  GroupInvitationDto,
  GroupMemberDto,
  GroupRequest,
  GroupSummaryDto,
} from '../types'

/** Grupos, membros e convites — docs/API_CONTRACTS.md §16 e §16.1. */
export const groupService = {
  async list(includeArchived = false): Promise<GroupSummaryDto[]> {
    const { data } = await http.get<GroupSummaryDto[]>('/groups', {
      params: includeArchived ? { includeArchived: true } : {},
    })
    return data
  },

  async create(request: GroupRequest): Promise<GroupDetailDto> {
    const { data } = await http.post<GroupDetailDto>('/groups', request)
    return data
  },

  async get(id: Uuid): Promise<GroupDetailDto> {
    const { data } = await http.get<GroupDetailDto>(`/groups/${id}`)
    return data
  },

  async update(id: Uuid, request: GroupRequest): Promise<GroupDetailDto> {
    const { data } = await http.put<GroupDetailDto>(`/groups/${id}`, request)
    return data
  },

  async archive(id: Uuid): Promise<GroupDetailDto> {
    const { data } = await http.post<GroupDetailDto>(`/groups/${id}/archive`)
    return data
  },

  async unarchive(id: Uuid): Promise<GroupDetailDto> {
    const { data } = await http.post<GroupDetailDto>(`/groups/${id}/unarchive`)
    return data
  },

  async leave(id: Uuid): Promise<void> {
    await http.post(`/groups/${id}/leave`)
  },

  async listMembers(id: Uuid, includeFormer = false): Promise<GroupMemberDto[]> {
    const { data } = await http.get<GroupMemberDto[]>(`/groups/${id}/members`, {
      params: includeFormer ? { includeFormer: true } : {},
    })
    return data
  },

  async changeRole(id: Uuid, memberId: Uuid, role: AssignableRole): Promise<GroupMemberDto> {
    const { data } = await http.put<GroupMemberDto>(`/groups/${id}/members/${memberId}/role`, { role })
    return data
  },

  async removeMember(id: Uuid, memberId: Uuid): Promise<void> {
    await http.delete(`/groups/${id}/members/${memberId}`)
  },

  async listInvitations(id: Uuid, status?: GroupInvitationStatus): Promise<GroupInvitationDto[]> {
    const { data } = await http.get<GroupInvitationDto[]>(`/groups/${id}/invitations`, {
      params: status ? { status } : {},
    })
    return data
  },

  async invite(id: Uuid, email: string): Promise<GroupInvitationDto> {
    const { data } = await http.post<GroupInvitationDto>(`/groups/${id}/invitations`, { email })
    return data
  },

  async resendInvitation(id: Uuid, invitationId: Uuid): Promise<GroupInvitationDto> {
    const { data } = await http.post<GroupInvitationDto>(`/groups/${id}/invitations/${invitationId}/resend`)
    return data
  },

  async revokeInvitation(id: Uuid, invitationId: Uuid): Promise<void> {
    await http.delete(`/groups/${id}/invitations/${invitationId}`)
  },
}
