import { ref, watch } from 'vue'
import { useClockStore } from '@/stores/clock'
import type { DashboardDto, DashboardQuery } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { dashboardService } from './services/dashboardService'

/** Carrega o dashboard sempre que o período mudar; descarta respostas obsoletas. */
export function useDashboard(getQuery: () => DashboardQuery) {
  const data = ref<DashboardDto | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  let sequence = 0

  async function reload(): Promise<void> {
    const current = ++sequence
    loading.value = true
    error.value = null
    try {
      const result = await dashboardService.get(getQuery())
      if (current !== sequence) return
      data.value = result
      // `today` vem do backend (D-021): passa a ser a referência das demais telas.
      useClockStore().setServerToday(result.today)
    } catch (e) {
      if (current !== sequence) return
      error.value = toAppError(e).message
    } finally {
      if (current === sequence) loading.value = false
    }
  }

  watch(() => `${getQuery().from}|${getQuery().to}`, reload, { immediate: true })

  return { data, loading, error, reload }
}
