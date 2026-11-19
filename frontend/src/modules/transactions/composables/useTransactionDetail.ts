import { ref } from 'vue'
import { transactionService } from '../services/transactionService'
import { toAppError } from '@/utils/problem'
import type { TransactionDetailDto, TransactionHistoryEntryDto } from '@/types/api'

/** Estado e chamadas do detalhe de uma movimentação (detalhe + histórico sob demanda). */
export function useTransactionDetail() {
  const detail = ref<TransactionDetailDto | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const history = ref<TransactionHistoryEntryDto[]>([])
  const historyLoading = ref(false)
  const historyError = ref<string | null>(null)
  const historyLoaded = ref(false)

  let sequence = 0

  async function load(id: string): Promise<void> {
    const current = ++sequence
    loading.value = true
    error.value = null
    history.value = []
    historyLoaded.value = false
    historyError.value = null
    try {
      const result = await transactionService.get(id)
      if (current === sequence) detail.value = result
    } catch (e) {
      if (current === sequence) {
        detail.value = null
        error.value = toAppError(e).message
      }
    } finally {
      if (current === sequence) loading.value = false
    }
  }

  async function loadHistory(): Promise<void> {
    if (!detail.value) return
    const id = detail.value.id
    historyLoading.value = true
    historyError.value = null
    try {
      const result = await transactionService.history(id)
      if (detail.value?.id === id) {
        history.value = result
        historyLoaded.value = true
      }
    } catch (e) {
      historyError.value = toAppError(e).message
    } finally {
      historyLoading.value = false
    }
  }

  /** Aplica um detalhe atualizado (resposta de pagamento/cancelamento) e invalida o histórico. */
  function apply(updated: TransactionDetailDto): void {
    detail.value = updated
    historyLoaded.value = false
    history.value = []
  }

  function reset(): void {
    sequence++
    detail.value = null
    error.value = null
    loading.value = false
    history.value = []
    historyLoaded.value = false
  }

  return {
    detail,
    loading,
    error,
    history,
    historyLoading,
    historyError,
    historyLoaded,
    load,
    loadHistory,
    apply,
    reset,
  }
}
