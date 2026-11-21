import { onBeforeUnmount, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useNotificationsStore } from './store'

/**
 * Atualiza o contador de não lidas (D-044, sem polling por timer): ao montar o app autenticado, ao trocar
 * de rota e ao voltar o foco à aba. O store limita a frequência e ignora chamadas sem sessão.
 */
export function useNotificationRefresh(): void {
  const store = useNotificationsStore()
  const route = useRoute()

  const refresh = () => void store.fetchCount()
  const onVisibility = () => {
    if (document.visibilityState === 'visible') refresh()
  }

  onMounted(() => {
    refresh()
    window.addEventListener('focus', refresh)
    document.addEventListener('visibilitychange', onVisibility)
  })

  onBeforeUnmount(() => {
    window.removeEventListener('focus', refresh)
    document.removeEventListener('visibilitychange', onVisibility)
  })

  watch(() => route.path, refresh)
}
