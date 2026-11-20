/**
 * Aceita apenas caminhos internos (começando com "/" e não "//" ou "/\") para evitar open redirect.
 */
export function safeRedirect(value: unknown, fallback = '/'): string {
  const candidate = Array.isArray(value) ? value[0] : value
  if (typeof candidate !== 'string') return fallback
  if (!candidate.startsWith('/') || candidate.startsWith('//') || candidate.startsWith('/\\')) return fallback
  return candidate
}

/** O destino de retorno é a página de aceite de um convite de grupo (`/invitations/:token`)? */
export function isInvitationRedirect(value: unknown): boolean {
  return safeRedirect(value, '').startsWith('/invitations/')
}
