import { reactive } from 'vue'

/**
 * Erros por campo de um formulário: combina validação local (UX) e `errors` do backend.
 * O backend continua sendo a fonte definitiva das regras.
 */
export function useFormErrors<K extends string>(fields: readonly K[]) {
  const errors = reactive({}) as Record<K, string | undefined>
  for (const field of fields) errors[field] = undefined

  function clear(field?: K): void {
    if (field) errors[field] = undefined
    else for (const f of fields) errors[f] = undefined
  }

  function setLocal(next: Partial<Record<K, string | undefined>>): boolean {
    clear()
    let valid = true
    for (const field of fields) {
      const message = next[field]
      if (message) {
        errors[field] = message
        valid = false
      }
    }
    return valid
  }

  /** Aplica erros do servidor (`fieldErrors`) aos campos conhecidos; retorna se algum foi aplicado. */
  function setServer(fieldErrors: Record<string, string[]>): boolean {
    let applied = false
    for (const field of fields) {
      const message = fieldErrors[field]?.[0]
      if (message) {
        errors[field] = message
        applied = true
      }
    }
    return applied
  }

  return { errors, clear, setLocal, setServer }
}
