import { http } from '@/services/http'
import type {
  AccountDto,
  BalanceSnapshotDto,
  CreateAccountRequest,
  CreateBalanceSnapshotRequest,
  DateOnlyString,
  UpdateAccountRequest,
  Uuid,
} from '@/types/api'

export interface ListAccountsOptions {
  includeInactive?: boolean
  asOf?: DateOnlyString
}

/** Contas — docs/API_CONTRACTS.md §6. */
export const accountService = {
  async list(options: ListAccountsOptions = {}): Promise<AccountDto[]> {
    const params: Record<string, string | boolean> = {}
    if (options.includeInactive) params.includeInactive = true
    if (options.asOf) params.asOf = options.asOf
    const { data } = await http.get<AccountDto[]>('/accounts', { params })
    return data
  },

  async get(id: Uuid, asOf?: DateOnlyString): Promise<AccountDto> {
    const { data } = await http.get<AccountDto>(`/accounts/${id}`, { params: asOf ? { asOf } : undefined })
    return data
  },

  async create(request: CreateAccountRequest): Promise<AccountDto> {
    const { data } = await http.post<AccountDto>('/accounts', request)
    return data
  },

  async update(id: Uuid, request: UpdateAccountRequest): Promise<AccountDto> {
    const { data } = await http.put<AccountDto>(`/accounts/${id}`, request)
    return data
  },

  async deactivate(id: Uuid): Promise<AccountDto> {
    const { data } = await http.post<AccountDto>(`/accounts/${id}/deactivate`)
    return data
  },

  async activate(id: Uuid): Promise<AccountDto> {
    const { data } = await http.post<AccountDto>(`/accounts/${id}/activate`)
    return data
  },

  async listSnapshots(id: Uuid): Promise<BalanceSnapshotDto[]> {
    const { data } = await http.get<BalanceSnapshotDto[]>(`/accounts/${id}/balance-snapshots`)
    return data
  },

  async createSnapshot(id: Uuid, request: CreateBalanceSnapshotRequest): Promise<BalanceSnapshotDto> {
    const { data } = await http.post<BalanceSnapshotDto>(`/accounts/${id}/balance-snapshots`, request)
    return data
  },
}
