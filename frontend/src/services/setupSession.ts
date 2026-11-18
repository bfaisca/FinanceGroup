import type { Router } from 'vue-router'
import { configureHttpSession } from '@/services/http'
import { useAuthStore } from '@/stores/auth'

/** Liga o cliente HTTP ao store de auth e ao router (evita dependência circular http ↔ store). */
export function setupHttpSession(router: Router): void {
  const auth = useAuthStore()

  configureHttpSession({
    getAccessToken: () => auth.accessToken,
    refreshSession: () => auth.refresh(),
    async onSessionExpired() {
      auth.clearSession()
      const current = router.currentRoute.value
      if (current.meta.requiresAuth) {
        await router.replace({ name: 'login', query: { redirect: current.fullPath, reason: 'expired' } })
      }
    },
  })
}
