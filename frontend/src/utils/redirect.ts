/**
 * Aceita apenas caminhos internos (começando com "/" e não "//" ou "/\") para evitar open redirect.
 */
export function safeRedirect(value: unknown, fallback = '/'): string {
  const candidate = Array.isArray(value) ? value[0] : value
  if (typeof candidate !== 'string') return fallback
  if (!candidate.startsWith('/') || candidate.startsWith('//') || candidate.startsWith('/\\')) return fallback
  return candidate
}
