import { ref, watch, type Ref } from 'vue'
import { toAppError } from '@/utils/problem'

/**
 * Carrega um relatório sempre que `getKey()` mudar e descarta respostas obsoletas.
 * `data` permanece (obsoleto) durante recargas e após erros, para a tela decidir como exibir.
 */
export function useReport<T>(fetcher: () => Promise<T>, getKey: () => string) {
  const data = ref<T | null>(null) as Ref<T | null>
  const loading = ref(false)
  const error = ref<string | null>(null)

  let sequence = 0

  async function reload(): Promise<void> {
    const current = ++sequence
    loading.value = true
    error.value = null
    try {
      const result = await fetcher()
      if (current !== sequence) return
      data.value = result
    } catch (e) {
      if (current !== sequence) return
      error.value = toAppError(e).message
    } finally {
      if (current === sequence) loading.value = false
    }
  }

  watch(getKey, reload, { immediate: true })

  return { data, loading, error, reload }
}
