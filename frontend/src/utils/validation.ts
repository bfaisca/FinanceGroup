/**
 * Validações de UX espelhando docs/API_CONTRACTS.md (seção 3).
 * O backend é a fonte definitiva: estas regras apenas antecipam erros comuns.
 * Cada função retorna a mensagem de erro ou `undefined` quando válido.
 */

export const NAME_MIN = 2
export const NAME_MAX = 100
export const EMAIL_MAX = 254
export const PASSWORD_MIN = 8
export const PASSWORD_MAX = 128

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const HAS_LETTER = /\p{L}/u
const HAS_DIGIT = /\p{Nd}/u

export function validateName(value: string): string | undefined {
  const length = value.trim().length
  if (length === 0) return 'Informe o nome.'
  if (length < NAME_MIN || length > NAME_MAX) return `O nome deve ter entre ${NAME_MIN} e ${NAME_MAX} caracteres.`
  return undefined
}

export function validateEmail(value: string): string | undefined {
  const email = value.trim()
  if (email.length === 0) return 'Informe o e-mail.'
  if (email.length > EMAIL_MAX || !EMAIL_PATTERN.test(email)) return 'Informe um e-mail válido.'
  return undefined
}

/** Senha obrigatória, sem aplicar política (ex.: login, senha atual). */
export function validateRequiredPassword(value: string): string | undefined {
  return value.length === 0 ? 'Informe a senha.' : undefined
}

/** Política de senha (D-007): 8–128 caracteres, ao menos uma letra e um número. */
export function validatePasswordPolicy(value: string): string | undefined {
  if (value.length === 0) return 'Informe a senha.'
  if (value.length < PASSWORD_MIN) return `A senha deve ter ao menos ${PASSWORD_MIN} caracteres.`
  if (value.length > PASSWORD_MAX) return `A senha deve ter no máximo ${PASSWORD_MAX} caracteres.`
  if (!HAS_LETTER.test(value) || !HAS_DIGIT.test(value)) return 'A senha deve conter ao menos uma letra e um número.'
  return undefined
}

export function validatePasswordConfirmation(password: string, confirmation: string): string | undefined {
  if (confirmation.length === 0) return 'Confirme a senha.'
  return password === confirmation ? undefined : 'As senhas não conferem.'
}

/** Normaliza e-mail como o backend (trim + minúsculas). */
export function normalizeEmail(value: string): string {
  return value.trim().toLowerCase()
}

/** Remove entradas vazias de um mapa de erros. */
export function compactErrors<K extends string>(errors: Record<K, string | undefined>): Partial<Record<K, string>> {
  const result: Partial<Record<K, string>> = {}
  for (const key of Object.keys(errors) as K[]) {
    const message = errors[key]
    if (message) result[key] = message
  }
  return result
}
