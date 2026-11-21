import { createPinia, type Pinia } from 'pinia'
import { useAuthStore } from '@/stores/auth'

/** Pinia com sessão autenticada (o store de notificações só consulta o backend com sessão). */
export function authedPinia(): Pinia {
  const pinia = createPinia()
  useAuthStore(pinia).setSession({
    accessToken: 't',
    accessTokenExpiresAt: '2026-11-21T23:00:00Z',
    user: { id: 'u1', name: 'Maria Silva', email: 'maria@exemplo.com', createdAt: '2026-01-01T00:00:00Z' },
  })
  return pinia
}

export function buttonByText(root: ParentNode, label: string | RegExp): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll('button')).find((b) =>
    typeof label === 'string' ? b.textContent?.trim() === label : label.test(b.textContent ?? ''),
  )
}
