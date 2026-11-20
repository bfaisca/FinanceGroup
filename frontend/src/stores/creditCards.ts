import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { creditCardService } from '@/modules/credit-cards/services/creditCardService'
import { toAppError } from '@/utils/problem'
import type { CreditCardDto, CreditCardRequest } from '@/types/api'

/** Cartões compartilhados por formulários (seletor "Pago com"), filtros, dashboard e tela de cartões (inclui inativos). */
export const useCreditCardsStore = defineStore('creditCards', () => {
  const items = ref<CreditCardDto[]>([])
  const loading = ref(false)
  const loaded = ref(false)
  const error = ref<string | null>(null)

  let inFlight: Promise<void> | null = null

  const activeItems = computed(() => items.value.filter((c) => c.isActive))
  const byId = computed(() => new Map(items.value.map((c) => [c.id, c])))

  function nameOf(id: string): string | undefined {
    return byId.value.get(id)?.name
  }

  function load(force = false): Promise<void> {
    if (loaded.value && !force) return Promise.resolve()
    if (inFlight) return inFlight
    loading.value = true
    error.value = null
    inFlight = (async () => {
      try {
        items.value = await creditCardService.list({ includeInactive: true })
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

  async function create(request: CreditCardRequest): Promise<CreditCardDto> {
    const created = await creditCardService.create(request)
    await load(true)
    return created
  }

  async function update(id: string, request: CreditCardRequest): Promise<CreditCardDto> {
    const updated = await creditCardService.update(id, request)
    await load(true)
    return updated
  }

  async function deactivate(id: string): Promise<CreditCardDto> {
    const result = await creditCardService.deactivate(id)
    await load(true)
    return result
  }

  async function activate(id: string): Promise<CreditCardDto> {
    const result = await creditCardService.activate(id)
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
