import type { LocationQuery, LocationQueryRaw } from 'vue-router'
import type {
  DateOnlyString,
  TransactionListQuery,
  TransactionSortField,
  TransactionStatus,
  TransactionType,
  Uuid,
} from '@/types/api'
import { parseDateOnly } from '@/utils/format'
import { TRANSACTION_STATUSES, TRANSACTION_TYPES } from '@/utils/labels'

export const DEFAULT_PAGE_SIZE = 20
export const MAX_PAGE_SIZE = 100
export const DEFAULT_SORT = '-transactionDate'
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100]

export const SORT_FIELDS: readonly TransactionSortField[] = [
  'transactionDate',
  'dueDate',
  'amount',
  'description',
  'createdAt',
]

/** Estado dos filtros da listagem — espelhado na query string da URL. */
export interface TransactionFilters {
  from?: DateOnlyString
  to?: DateOnlyString
  type?: TransactionType
  categoryId?: Uuid
  accountId?: Uuid
  status?: TransactionStatus
  search?: string
  page: number
  pageSize: number
  sort: string
}

export const SORT_OPTIONS: readonly { value: string; label: string }[] = [
  { value: '-transactionDate', label: 'Data (mais recente)' },
  { value: 'transactionDate', label: 'Data (mais antiga)' },
  { value: 'dueDate', label: 'Vencimento (mais próximo)' },
  { value: '-dueDate', label: 'Vencimento (mais distante)' },
  { value: '-amount', label: 'Valor (maior)' },
  { value: 'amount', label: 'Valor (menor)' },
  { value: 'description', label: 'Descrição (A–Z)' },
  { value: '-description', label: 'Descrição (Z–A)' },
]

export function defaultFilters(overrides: Partial<TransactionFilters> = {}): TransactionFilters {
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
export function parseTransactionFilters(query: LocationQuery): TransactionFilters {
  const filters = defaultFilters()

  const from = single(query.from)
  if (from && parseDateOnly(from)) filters.from = from
  const to = single(query.to)
  if (to && parseDateOnly(to)) filters.to = to

  const type = single(query.type)
  if (type && (TRANSACTION_TYPES as string[]).includes(type)) filters.type = type as TransactionType
  const status = single(query.status)
  if (status && (TRANSACTION_STATUSES as string[]).includes(status)) filters.status = status as TransactionStatus

  const categoryId = single(query.categoryId)
  if (categoryId) filters.categoryId = categoryId
  const accountId = single(query.accountId)
  if (accountId) filters.accountId = accountId
  const search = single(query.search)
  if (search) filters.search = search

  filters.page = positiveInt(single(query.page)) ?? 1
  const pageSize = positiveInt(single(query.pageSize))
  if (pageSize) filters.pageSize = Math.min(pageSize, MAX_PAGE_SIZE)
  filters.sort = validSort(single(query.sort)) ?? DEFAULT_SORT

  return filters
}

/** Query string correspondente aos filtros; omite valores padrão para manter a URL curta. */
export function toRouteQuery(filters: TransactionFilters): Record<string, string> {
  const query: Record<string, string> = {}
  if (filters.from) query.from = filters.from
  if (filters.to) query.to = filters.to
  if (filters.type) query.type = filters.type
  if (filters.status) query.status = filters.status
  if (filters.categoryId) query.categoryId = filters.categoryId
  if (filters.accountId) query.accountId = filters.accountId
  if (filters.search?.trim()) query.search = filters.search.trim()
  if (filters.page > 1) query.page = String(filters.page)
  if (filters.pageSize !== DEFAULT_PAGE_SIZE) query.pageSize = String(filters.pageSize)
  if (filters.sort !== DEFAULT_SORT) query.sort = filters.sort
  return query
}

/** Mescla a query de filtros com parâmetros alheios à listagem (ex.: `open`, `new`) preservados. */
export function mergeRouteQuery(current: LocationQuery, filters: TransactionFilters): LocationQueryRaw {
  const merged: LocationQueryRaw = {}
  for (const key of ['open', 'new']) {
    const value = single(current[key])
    if (value) merged[key] = value
  }
  return { ...merged, ...toRouteQuery(filters) }
}

/** Parâmetros de `GET /transactions` a partir dos filtros. */
export function toListQuery(filters: TransactionFilters): TransactionListQuery {
  const query: TransactionListQuery = { page: filters.page, pageSize: filters.pageSize, sort: filters.sort }
  if (filters.from) query.from = filters.from
  if (filters.to) query.to = filters.to
  if (filters.type) query.type = filters.type
  if (filters.status) query.status = filters.status
  if (filters.categoryId) query.categoryId = filters.categoryId
  if (filters.accountId) query.accountId = filters.accountId
  if (filters.search?.trim()) query.search = filters.search.trim()
  return query
}

/** Quantidade de filtros ativos (exclui paginação e ordenação). */
export function countActiveFilters(filters: TransactionFilters): number {
  return [
    filters.from,
    filters.to,
    filters.type,
    filters.status,
    filters.categoryId,
    filters.accountId,
    filters.search?.trim(),
  ].filter(Boolean).length
}
