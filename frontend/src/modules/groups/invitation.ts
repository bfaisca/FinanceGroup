import type { InvitationPreviewDto } from './types'

export type InvitationState =
  'actionable' | 'alreadyMember' | 'wrongEmail' | 'expired' | 'revoked' | 'accepted' | 'declined'

/**
 * Estado de apresentação do convite (§16.1). A decisão final é sempre do backend (`canAccept`):
 * `Pending` sem `canAccept` só pode ser e-mail diferente (expiração já chega como `Expired`).
 */
export function invitationState(
  preview: Pick<InvitationPreviewDto, 'status' | 'canAccept' | 'alreadyMember'>,
): InvitationState {
  if (preview.alreadyMember) return 'alreadyMember'
  switch (preview.status) {
    case 'Pending':
      return preview.canAccept ? 'actionable' : 'wrongEmail'
    case 'Expired':
      return 'expired'
    case 'Revoked':
      return 'revoked'
    case 'Accepted':
      return 'accepted'
    case 'Declined':
      return 'declined'
  }
}
