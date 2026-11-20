import { http } from '@/services/http'
import type {
  DateOnlyString,
  ReportByAccountDto,
  ReportByCategoryDto,
  ReportByPaymentMethodDto,
  ReportGroupBy,
  ReportInstallmentsDto,
  ReportMonthlyEvolutionDto,
  ReportRecurrencesDto,
  ReportSummaryDto,
  TransactionType,
} from '@/types/api'

export interface ReportPeriodQuery {
  from: DateOnlyString
  to: DateOnlyString
}

export interface ReportTypedQuery extends ReportPeriodQuery {
  /** Padrão do backend: `Expense`. */
  type?: TransactionType
}

function periodParams(query: ReportPeriodQuery): Record<string, string> {
  return { from: query.from, to: query.to }
}

function typedParams(query: ReportTypedQuery): Record<string, string> {
  const params = periodParams(query)
  if (query.type) params.type = query.type
  return params
}

/** Relatórios — docs/API_CONTRACTS.md §14. */
export const reportService = {
  async summary(query: ReportPeriodQuery & { groupBy?: ReportGroupBy }): Promise<ReportSummaryDto> {
    const params: Record<string, string> = periodParams(query)
    if (query.groupBy) params.groupBy = query.groupBy
    const { data } = await http.get<ReportSummaryDto>('/reports/summary', { params })
    return data
  },

  async byCategory(query: ReportTypedQuery): Promise<ReportByCategoryDto> {
    const { data } = await http.get<ReportByCategoryDto>('/reports/by-category', { params: typedParams(query) })
    return data
  },

  async byAccount(query: ReportTypedQuery): Promise<ReportByAccountDto> {
    const { data } = await http.get<ReportByAccountDto>('/reports/by-account', { params: typedParams(query) })
    return data
  },

  async byPaymentMethod(query: ReportTypedQuery): Promise<ReportByPaymentMethodDto> {
    const { data } = await http.get<ReportByPaymentMethodDto>('/reports/by-payment-method', {
      params: typedParams(query),
    })
    return data
  },

  /** `endMonth` ("YYYY-MM") é opcional: o backend usa o mês atual. */
  async monthlyEvolution(query: { months?: number; endMonth?: string } = {}): Promise<ReportMonthlyEvolutionDto> {
    const params: Record<string, string | number> = {}
    if (query.months !== undefined) params.months = query.months
    if (query.endMonth) params.endMonth = query.endMonth
    const { data } = await http.get<ReportMonthlyEvolutionDto>('/reports/monthly-evolution', { params })
    return data
  },

  async installments(query: { months?: number } = {}): Promise<ReportInstallmentsDto> {
    const params: Record<string, number> = {}
    if (query.months !== undefined) params.months = query.months
    const { data } = await http.get<ReportInstallmentsDto>('/reports/installments', { params })
    return data
  },

  async recurrences(): Promise<ReportRecurrencesDto> {
    const { data } = await http.get<ReportRecurrencesDto>('/reports/recurrences')
    return data
  },
}
