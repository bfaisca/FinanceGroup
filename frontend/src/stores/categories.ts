import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { categoryService } from '@/modules/categories/services/categoryService'
import { toAppError } from '@/utils/problem'
import type { CategoryDto, CreateCategoryRequest, UpdateCategoryRequest } from '@/types/api'

/**
 * Categorias compartilhadas por formulários, filtros e pela tela de categorias.
 * Mantém a lista plana (incluindo inativas); a árvore é derivada no cliente.
 */
export const useCategoriesStore = defineStore('categories', () => {
  const items = ref<CategoryDto[]>([])
  const loading = ref(false)
  const loaded = ref(false)
  const error = ref<string | null>(null)

  let inFlight: Promise<void> | null = null

  const activeItems = computed(() => items.value.filter((c) => c.isActive))
  const byId = computed(() => new Map(items.value.map((c) => [c.id, c])))

  function nameOf(id: string): string | undefined {
    return byId.value.get(id)?.name
  }

  /** Carrega a lista uma vez (ou de novo com `force`). Chamadas concorrentes compartilham a requisição. */
  function load(force = false): Promise<void> {
    if (loaded.value && !force) return Promise.resolve()
    if (inFlight) return inFlight
    loading.value = true
    error.value = null
    inFlight = (async () => {
      try {
        items.value = await categoryService.list({ includeInactive: true })
        loaded.value = true
      } catch (e) {
        error.value = toAppError(e).message
      } finally {
        loading.value = false
        inFlight = null
      }
    })()
    return inFlight
  }

  async function create(request: CreateCategoryRequest): Promise<CategoryDto> {
    const created = await categoryService.create(request)
    await load(true)
    return created
  }

  async function update(id: string, request: UpdateCategoryRequest): Promise<CategoryDto> {
    const updated = await categoryService.update(id, request)
    await load(true)
    return updated
  }

  /** Desativar uma categoria também desativa suas subcategorias: recarrega a lista. */
  async function deactivate(id: string): Promise<CategoryDto> {
    const result = await categoryService.deactivate(id)
    await load(true)
    return result
  }

  async function activate(id: string): Promise<CategoryDto> {
    const result = await categoryService.activate(id)
    await load(true)
    return result
  }

  function reset(): void {
    items.value = []
    loaded.value = false
    error.value = null
  }

  return { items, loading, loaded, error, activeItems, byId, nameOf, load, create, update, deactivate, activate, reset }
})
