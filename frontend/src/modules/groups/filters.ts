import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import type { DateOnlyString, TransactionStatus, Uuid } from '@/types/api'
import { parseDateOnly } from '@/utils/format'
import { TRANSACTION_STATUSES } from '@/utils/labels'
import { DEFAULT_PAGE_SIZE, DEFAULT_SORT, MAX_PAGE_SIZE, SORT_FIELDS } from '@/modules/transactions/filters'
import type { GroupTransactionListQuery } from './types'

/** Estado dos filtros da listagem de movimentos do grupo — espelhado na query string da URL. */
export interface GroupTransactionFilters {
  from?: DateOnlyString
  to?: DateOnlyString
  categoryId?: Uuid
  status?: TransactionStatus
  search?: string
  createdByUserId?: Uuid
  paidByUserId?: Uuid
  page: number
  pageSize: number
  sort: string
}

/** Parâmetros da URL que pertencem à página (aba, detalhe aberto, criação) e não aos filtros. */
const PAGE_PARAMS = ['tab', 'open', 'new'] as const

export function defaultGroupFilters(overrides: Partial<GroupTransactionFilters> = {}): GroupTransactionFilters {
  return { page: 1, pageSize: DEFAULT_PAGE_SIZE, sort: DEFAULT_SORT, ...overrides }
}

function single(value: LocationQuery[string] | undefined): string | undefined {
  const first = Array.isArray(value) ? value[0] : value
  return typeof first === 'string' && first.trim().length > 0 ? first.trim() : undefined
}

function positiveInt(value: string | undefined): number | undefined {
  if (!value || !/^\d+$/.test(value)) return undefined
  const n = Number(value)
  return Number.isSafeInteger(n) && n > 0 ? n : undefined
}

function validSort(value: string | undefined): string | undefined {
  if (!value) return undefined
  const field = value.startsWith('-') ? value.slice(1) : value
  return (SORT_FIELDS as readonly string[]).includes(field) ? value : undefined
}

/** Lê os filtros da query string; valores inválidos são ignorados (nunca quebram a tela). */
export function parseGroupTransactionFilters(query: LocationQuery): GroupTransactionFilters {
  const filters = defaultGroupFilters()

  const from = single(query.from)
  if (from && parseDateOnly(from)) filters.from = from
  const to = single(query.to)
  if (to && parseDateOnly(to)) filters.to = to

  const status = single(query.status)
  if (status && (TRANSACTION_STATUSES as string[]).includes(status)) filters.status = status as TransactionStatus

  const categoryId = single(query.categoryId)
  if (categoryId) filters.categoryId = categoryId
  const createdByUserId = single(query.createdBy)
  if (createdByUserId) filters.createdByUserId = createdByUserId
  const paidByUserId = single(query.paidBy)
  if (paidByUserId) filters.paidByUserId = paidByUserId
  const search = single(query.search)
  if (search) filters.search = search

  filters.page = positiveInt(single(query.page)) ?? 1
  const pageSize = positiveInt(single(query.pageSize))
  if (pageSize) filters.pageSize = Math.min(pageSize, MAX_PAGE_SIZE)
  filters.sort = validSort(single(query.sort)) ?? DEFAULT_SORT
  return filters
}

/** Query string correspondente aos filtros; omite valores padrão. `createdBy`/`paidBy` são nomes de URL. */
export function toGroupRouteQuery(filters: GroupTransactionFilters): Record<string, string> {
  const query: Record<string, string> = {}
  if (filters.from) query.from = filters.from
  if (filters.to) query.to = filters.to
  if (filters.status) query.status = filters.status
  if (filters.categoryId) query.categoryId = filters.categoryId
  if (filters.createdByUserId) query.createdBy = filters.createdByUserId
  if (filters.paidByUserId) query.paidBy = filters.paidByUserId
  if (filters.search?.trim()) query.search = filters.search.trim()
  if (filters.page > 1) query.page = String(filters.page)
  if (filters.pageSize !== DEFAULT_PAGE_SIZE) query.pageSize = String(filters.pageSize)
  if (filters.sort !== DEFAULT_SORT) query.sort = filters.sort
  return query
}

/** Mescla os filtros com parâmetros da página (`tab`, `open`, `new`) preservados. */
export function mergeGroupRouteQuery(current: LocationQuery, filters: GroupTransactionFilters): LocationQueryRaw {
  const merged: LocationQueryRaw = {}
  for (const key of PAGE_PARAMS) {
    const value = single(current[key])
    if (value) merged[key] = value
  }
  return { ...merged, ...toGroupRouteQuery(filters) }
}

/** Parâmetros de `GET /groups/{id}/transactions` a partir dos filtros. */
export function toGroupListQuery(filters: GroupTransactionFilters): GroupTransactionListQuery {
  const query: GroupTransactionListQuery = { page: filters.page, pageSize: filters.pageSize, sort: filters.sort }
  if (filters.from) query.from = filters.from
  if (filters.to) query.to = filters.to
  if (filters.status) query.status = filters.status
  if (filters.categoryId) query.categoryId = filters.categoryId
  if (filters.createdByUserId) query.createdByUserId = filters.createdByUserId
  if (filters.paidByUserId) query.paidByUserId = filters.paidByUserId
  if (filters.search?.trim()) query.search = filters.search.trim()
  return query
}

/** Quantidade de filtros ativos (exclui paginação e ordenação). */
export function countActiveGroupFilters(filters: GroupTransactionFilters): number {
  return [
    filters.from,
    filters.to,
    filters.status,
    filters.categoryId,
    filters.createdByUserId,
    filters.paidByUserId,
    filters.search?.trim(),
  ].filter(Boolean).length
}
