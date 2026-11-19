import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { accountService } from '@/modules/accounts/services/accountService'
import { useClockStore } from '@/stores/clock'
import { toAppError } from '@/utils/problem'
import type { AccountDto, CreateAccountRequest, UpdateAccountRequest } from '@/types/api'

/** Contas compartilhadas por formulários, filtros e pela tela de contas (inclui inativas). */
export const useAccountsStore = defineStore('accounts', () => {
  const items = ref<AccountDto[]>([])
  const loading = ref(false)
  const loaded = ref(false)
  const error = ref<string | null>(null)

  let inFlight: Promise<void> | null = null

  const activeItems = computed(() => items.value.filter((a) => a.isActive))
  const byId = computed(() => new Map(items.value.map((a) => [a.id, a])))

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
        const list = await accountService.list({ includeInactive: true })
        items.value = list
        loaded.value = true
        // Sem `asOf`, `projectedAsOf` é o "hoje" da aplicação (D-021).
        const first = list[0]
        if (first) useClockStore().setServerToday(first.projectedAsOf)
      } catch (e) {
        error.value = toAppError(e).message
      } finally {
        loading.value = false
        inFlight = null
      }
    })()
    return inFlight
  }

  async function create(request: CreateAccountRequest): Promise<AccountDto> {
    const created = await accountService.create(request)
    await load(true)
    return created
  }

  async function update(id: string, request: UpdateAccountRequest): Promise<AccountDto> {
    const updated = await accountService.update(id, request)
    await load(true)
    return updated
  }

  async function deactivate(id: string): Promise<AccountDto> {
    const result = await accountService.deactivate(id)
    await load(true)
    return result
  }

  async function activate(id: string): Promise<AccountDto> {
    const result = await accountService.activate(id)
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
