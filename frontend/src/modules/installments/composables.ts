import { ref, watch } from 'vue'
import type { InstallmentPlanDto, InstallmentPlanListQuery } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { installmentPlanService } from './services/installmentPlanService'

/** Lista paginada de planos; descarta respostas obsoletas quando a query muda. */
export function useInstallmentPlanList(getQuery: () => InstallmentPlanListQuery) {
  const items = ref<InstallmentPlanDto[]>([])
  const totalCount = ref(0)
  const totalPages = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  let sequence = 0

  async function reload(): Promise<void> {
    const current = ++sequence
    loading.value = true
    error.value = null
    try {
      const result = await installmentPlanService.list(getQuery())
      if (current !== sequence) return
      items.value = result.items
      totalCount.value = result.totalCount
      totalPages.value = result.totalPages
    } catch (e) {
      if (current !== sequence) return
      error.value = toAppError(e).message
    } finally {
      if (current === sequence) loading.value = false
    }
  }

  watch(() => JSON.stringify(getQuery()), reload, { immediate: true })

  return { items, totalCount, totalPages, loading, error, reload }
}
