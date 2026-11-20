import type { GroupInvitationStatus, GroupMemberRole, GroupMemberStatus } from '@/types/api'
import type { StatusPresentation } from '@/utils/labels'

export const ROLE_LABELS: Record<GroupMemberRole, string> = {
  Owner: 'Proprietário',
  Admin: 'Administrador',
  Member: 'Membro',
}

export const ROLE_ICONS: Record<GroupMemberRole, string> = {
  Owner: 'pi pi-crown',
  Admin: 'pi pi-shield',
  Member: 'pi pi-user',
}

export const MEMBER_STATUS_LABELS: Record<GroupMemberStatus, string> = {
  Active: 'Ativo',
  Left: 'Saiu do grupo',
  Removed: 'Removido',
}

export const INVITATION_STATUS_PRESENTATION: Record<GroupInvitationStatus, StatusPresentation> = {
  Pending: { label: 'Pendente', icon: 'pi pi-clock', tone: 'warning' },
  Accepted: { label: 'Aceito', icon: 'pi pi-check-circle', tone: 'success' },
  Declined: { label: 'Recusado', icon: 'pi pi-times-circle', tone: 'neutral' },
  Revoked: { label: 'Revogado', icon: 'pi pi-ban', tone: 'neutral' },
  Expired: { label: 'Expirado', icon: 'pi pi-exclamation-circle', tone: 'danger' },
}

export const INVITATION_STATUSES = Object.keys(INVITATION_STATUS_PRESENTATION) as GroupInvitationStatus[]

/** Marca quem não é mais membro ativo (nome vem do backend, com "Usuário removido" quando anonimizado). */
export const FORMER_MEMBER_SUFFIX = '(ex-membro)'
