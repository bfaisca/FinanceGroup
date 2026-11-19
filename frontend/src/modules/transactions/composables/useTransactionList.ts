import { ref, watch } from 'vue'
import { transactionService } from '../services/transactionService'
import { toAppError } from '@/utils/problem'
import type { TransactionDto, TransactionListQuery } from '@/types/api'

/**
 * Carrega a listagem paginada de movimentações sempre que a query mudar.
 * Respostas fora de ordem (requisição antiga terminando depois da nova) são descartadas.
 */
export function useTransactionList(getQuery: () => TransactionListQuery) {
  const items = ref<TransactionDto[]>([])
  const totalCount = ref(0)
  const totalPages = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)
  const loadedOnce = ref(false)

  let sequence = 0

  async function reload(): Promise<void> {
    const current = ++sequence
    loading.value = true
    error.value = null
    try {
      const result = await transactionService.list(getQuery())
      if (current !== sequence) return
      items.value = result.items
      totalCount.value = result.totalCount
      totalPages.value = result.totalPages
      loadedOnce.value = true
    } catch (e) {
      if (current !== sequence) return
      error.value = toAppError(e).message
    } finally {
      if (current === sequence) loading.value = false
    }
  }

  watch(() => JSON.stringify(getQuery()), reload, { immediate: true })

  return { items, totalCount, totalPages, loading, error, loadedOnce, reload }
}
