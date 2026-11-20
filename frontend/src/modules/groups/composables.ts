import { ref, watch, type Ref } from 'vue'
import { toAppError } from '@/utils/problem'
import { groupTransactionService } from './services/groupTransactionService'
import type { GroupTransactionDetailDto, GroupTransactionHistoryEntryDto } from './types'

export interface Resource<T> {
  data: Ref<T | null>
  loading: Ref<boolean>
  error: Ref<string | null>
  /** Status HTTP do último erro (ex.: 404 = grupo inexistente ou sem participação ativa). */
  errorStatus: Ref<number | null>
  loadedOnce: Ref<boolean>
  reload: () => Promise<void>
}

/**
 * Carrega um recurso sempre que `key` mudar (`null` = não carregar). Respostas fora de ordem
 * (requisição antiga terminando depois da nova) são descartadas. Com `clearOnKeyChange`, a chave
 * nova descarta o dado anterior (ex.: outro grupo); sem ele, o dado antigo permanece enquanto recarrega.
 */
export function useResource<T>(
  fetcher: () => Promise<T>,
  key: () => string | null,
  options: { clearOnKeyChange?: boolean } = {},
): Resource<T> {
  const data = ref<T | null>(null) as Ref<T | null>
  const loading = ref(false)
  const error = ref<string | null>(null)
  const errorStatus = ref<number | null>(null)
  const loadedOnce = ref(false)

  let sequence = 0

  async function reload(): Promise<void> {
    if (key() === null) return
    const current = ++sequence
    loading.value = true
    error.value = null
    errorStatus.value = null
    try {
      const result = await fetcher()
      if (current !== sequence) return
      data.value = result
      loadedOnce.value = true
    } catch (e) {
      if (current !== sequence) return
      const appError = toAppError(e)
      error.value = appError.message
      errorStatus.value = appError.status
    } finally {
      if (current === sequence) loading.value = false
    }
  }

  watch(
    key,
    (value, previous) => {
      if (value === null) {
        sequence++
        data.value = null
        loading.value = false
        return
      }
      if (options.clearOnKeyChange && previous !== undefined && previous !== value) {
        data.value = null
        loadedOnce.value = false
      }
      void reload()
    },
    { immediate: true },
  )

  return { data, loading, error, errorStatus, loadedOnce, reload }
}

/** Estado e chamadas do detalhe de uma movimentação do grupo (detalhe + histórico sob demanda). */
export function useGroupTransactionDetail(getGroupId: () => string) {
  const detail = ref<GroupTransactionDetailDto | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const history = ref<GroupTransactionHistoryEntryDto[]>([])
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
      const result = await groupTransactionService.get(getGroupId(), id)
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
      const result = await groupTransactionService.history(getGroupId(), id)
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
  function apply(updated: GroupTransactionDetailDto): void {
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
