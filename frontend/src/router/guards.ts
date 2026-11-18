import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

export const APP_NAME = 'GroupFinance'

/**
 * Guard global de autenticação. Aguarda o bootstrap da sessão (refresh via cookie)
 * antes de decidir, para não redirecionar ao login um usuário com sessão válida.
 */
export async function authGuard(to: RouteLocationNormalized): Promise<true | RouteLocationRaw> {
  const auth = useAuthStore()
  await auth.bootstrap()

  if (to.matched.some((r) => r.meta.requiresAuth) && !auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  if (to.matched.some((r) => r.meta.guestOnly) && auth.isAuthenticated) {
    return { name: 'dashboard' }
  }

  return true
}

export function pageTitle(to: RouteLocationNormalized): string {
  const title = [...to.matched].reverse().find((r) => r.meta.title)?.meta.title
  return title ? `${title} · ${APP_NAME}` : APP_NAME
}
