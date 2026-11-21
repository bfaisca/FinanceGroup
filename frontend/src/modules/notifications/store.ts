import { computed, ref, type Ref } from 'vue'
import { defineStore } from 'pinia'
import type { GroupSummaryDto } from '@/modules/groups/types'
import { useAuthStore } from '@/stores/auth'
import { toAppError } from '@/utils/problem'
import { notificationService } from './services/notificationService'
import type { NotificationDto, NotificationListQuery } from './types'

/** Intervalo mínimo entre buscas do contador disparadas por eventos (rota/foco). D-044: sem polling por timer. */
export const COUNT_MIN_INTERVAL_MS = 5000
export const RECENT_PAGE_SIZE = 8

export interface NotificationListState {
  items: NotificationDto[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  loading: boolean
  error: string | null
  loaded: boolean
}

function emptyList(pageSize: number): NotificationListState {
  return { items: [], page: 1, pageSize, totalCount: 0, totalPages: 0, loading: false, error: null, loaded: false }
}

/**
 * Notificações dentro do app (D-041 a D-044). Mantém o contador de não lidas e duas listas:
 * `recent` (painel do sino) e `full` (página /notifications). Marcar como lida é otimista e reverte em erro.
 */
export const useNotificationsStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  const recent = ref<NotificationListState>(emptyList(RECENT_PAGE_SIZE))
  const full = ref<NotificationListState>(emptyList(20))
  const fullQuery = ref<NotificationListQuery | null>(null)

  const hasUnread = computed(() => unreadCount.value > 0)

  // Controle interno (não reativo): descarta respostas atrasadas e limita requisições.
  let generation = 0
  let mutationVersion = 0
  let countInFlight: Promise<void> | null = null
  let lastCountAt = 0
  const listSeq = { recent: 0, full: 0 }

  function sessionActive(): boolean {
    return useAuthStore().isAuthenticated
  }

  /**
   * Busca o contador. Sem sessão não faz nada; chamadas simultâneas compartilham a requisição e, sem `force`,
   * chamadas dentro de `COUNT_MIN_INTERVAL_MS` da última são ignoradas. Falhas mantêm o valor atual.
   */
  function fetchCount(options: { force?: boolean } = {}): Promise<void> {
    if (!sessionActive()) return Promise.resolve()
    if (countInFlight) return countInFlight
    const now = Date.now()
    if (!options.force && now - lastCountAt < COUNT_MIN_INTERVAL_MS) return Promise.resolve()
    lastCountAt = now
    const gen = generation
    const version = mutationVersion
    countInFlight = (async () => {
      try {
        const count = await notificationService.unreadCount()
        // Uma ação local (otimista) ou o logout ocorrida durante a busca torna a resposta obsoleta.
        if (gen === generation && version === mutationVersion) unreadCount.value = count
      } catch {
        // Mantém o contador anterior; a próxima oportunidade tenta novamente.
      } finally {
        countInFlight = null
      }
    })()
    return countInFlight
  }

  async function loadList(
    key: 'recent' | 'full',
    target: Ref<NotificationListState>,
    query: NotificationListQuery,
  ): Promise<void> {
    if (!sessionActive()) return
    const seq = ++listSeq[key]
    const gen = generation
    target.value.loading = true
    target.value.error = null
    try {
      const result = await notificationService.list(query)
      if (seq !== listSeq[key] || gen !== generation) return
      target.value = {
        items: result.items,
        page: result.page,
        pageSize: result.pageSize,
        totalCount: result.totalCount,
        totalPages: result.totalPages,
        loading: false,
        error: null,
        loaded: true,
      }
    } catch (e) {
      if (seq !== listSeq[key] || gen !== generation) return
      target.value.error = toAppError(e).message
      target.value.loading = false
    }
  }

  /** Painel do sino: as notificações mais recentes. */
  function fetchRecent(): Promise<void> {
    return loadList('recent', recent, { page: 1, pageSize: RECENT_PAGE_SIZE })
  }

  /** Página completa (paginada, com filtro opcional de não lidas). */
  function fetchPage(query: NotificationListQuery): Promise<void> {
    fullQuery.value = query
    return loadList('full', full, query)
  }

  function lists(): Ref<NotificationListState>[] {
    return [recent, full]
  }

  function findItem(id: string): NotificationDto | undefined {
    for (const list of lists()) {
      const found = list.value.items.find((i) => i.id === id)
      if (found) return found
    }
    return undefined
  }

  function patchItem(id: string, patch: (n: NotificationDto) => NotificationDto): void {
    for (const list of lists()) {
      list.value.items = list.value.items.map((i) => (i.id === id ? patch(i) : i))
    }
  }

  function markReadLocal(id: string): void {
    patchItem(id, (n) => ({ ...n, isRead: true, readAt: n.readAt ?? new Date().toISOString() }))
  }

  /** Recarrega as listas já abertas (após conflito/erro, o estado do servidor é a verdade). */
  function reloadLoaded(): void {
    if (recent.value.loaded) void fetchRecent()
    if (full.value.loaded && fullQuery.value) void fetchPage(fullQuery.value)
  }

  /** Marca uma notificação como lida (otimista; em erro reverte e reconcilia). Retorna `false` se falhou. */
  async function markRead(notification: NotificationDto): Promise<boolean> {
    const current = findItem(notification.id) ?? notification
    if (current.isRead) return true
    mutationVersion++
    markReadLocal(current.id)
    unreadCount.value = Math.max(0, unreadCount.value - 1)
    try {
      await notificationService.markRead(current.id)
      return true
    } catch {
      mutationVersion++
      patchItem(current.id, (n) => ({ ...n, isRead: false, readAt: null }))
      unreadCount.value += 1
      void fetchCount({ force: true })
      return false
    }
  }

  /** Marca todas como lidas (otimista; em erro restaura o estado anterior). Retorna `false` se falhou. */
  async function markAllRead(): Promise<boolean> {
    mutationVersion++
    const snapshot = { count: unreadCount.value, recent: recent.value.items, full: full.value.items }
    const now = new Date().toISOString()
    const readAll = (items: NotificationDto[]) => items.map((n) => (n.isRead ? n : { ...n, isRead: true, readAt: now }))
    recent.value.items = readAll(recent.value.items)
    full.value.items = readAll(full.value.items)
    unreadCount.value = 0
    try {
      await notificationService.markAllRead()
      return true
    } catch {
      mutationVersion++
      recent.value.items = snapshot.recent
      full.value.items = snapshot.full
      unreadCount.value = snapshot.count
      void fetchCount({ force: true })
      return false
    }
  }

  async function respondInvitation<T>(
    notification: NotificationDto,
    call: () => Promise<T>,
    status: 'Accepted' | 'Declined',
  ): Promise<T> {
    let result: T
    try {
      result = await call()
    } catch (e) {
      // 403/404/422: o convite pode ter mudado de estado; reconcilia e deixa o chamador exibir o `detail`.
      void fetchCount({ force: true })
      reloadLoaded()
      throw e
    }
    const current = findItem(notification.id) ?? notification
    mutationVersion++
    if (!current.isRead) unreadCount.value = Math.max(0, unreadCount.value - 1)
    patchItem(current.id, (n) => ({
      ...n,
      isRead: true,
      readAt: n.readAt ?? new Date().toISOString(),
      invitation: n.invitation ? { ...n.invitation, status, canRespond: false } : n.invitation,
    }))
    return result
  }

  /** Aceita o convite pela notificação (marca como lida). Lança o erro da API para o chamador tratar. */
  function acceptInvitation(notification: NotificationDto): Promise<GroupSummaryDto> {
    return respondInvitation(notification, () => notificationService.acceptInvitation(notification.id), 'Accepted')
  }

  function declineInvitation(notification: NotificationDto): Promise<void> {
    return respondInvitation(notification, () => notificationService.declineInvitation(notification.id), 'Declined')
  }

  /** Logout/sessão encerrada: descarta tudo e invalida respostas em andamento. */
  function reset(): void {
    generation++
    mutationVersion++
    countInFlight = null
    lastCountAt = 0
    listSeq.recent++
    listSeq.full++
    unreadCount.value = 0
    recent.value = emptyList(RECENT_PAGE_SIZE)
    full.value = emptyList(20)
    fullQuery.value = null
  }

  return {
    unreadCount,
    hasUnread,
    recent,
    full,
    fetchCount,
    fetchRecent,
    fetchPage,
    markRead,
    markAllRead,
    acceptInvitation,
    declineInvitation,
    reset,
  }
})
