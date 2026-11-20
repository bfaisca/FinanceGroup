import type { GroupMemberRole } from '@/types/api'
import type { GroupMemberDto } from './types'

export interface MemberActions {
  canPromote: boolean
  canDemote: boolean
  canRemove: boolean
}

const NONE: MemberActions = { canPromote: false, canDemote: false, canRemove: false }

/**
 * Ações oferecidas na linha de um membro (D-030). É só UX: o backend decide (`403`/`422`).
 * Admin promove e remove Members; só o Owner rebaixa Admin e remove Admin; o Owner nunca é alterado
 * e ninguém se remove (sair do grupo é outra ação).
 */
export function memberActions(
  myRole: GroupMemberRole,
  canManageMembers: boolean,
  member: GroupMemberDto,
): MemberActions {
  if (!canManageMembers || member.isMe || member.status !== 'Active' || member.role === 'Owner') return NONE
  const iAmOwner = myRole === 'Owner'
  return {
    canPromote: member.role === 'Member',
    canDemote: iAmOwner && member.role === 'Admin',
    canRemove: member.role === 'Member' || iAmOwner,
  }
}
