import type { AccountDto, CategoryDto, DashboardDto, TransactionDetailDto, TransactionDto } from '@/types/api'

/** Fixtures usadas SOMENTE em testes (nunca importar em código de produção). */
export function makeTransaction(partial: Partial<TransactionDto> = {}): TransactionDto {
  return {
    id: 't1',
    description: 'Aluguel',
    type: 'Expense',
    amount: 1500,
    paidAmount: 0,
    remainingAmount: 1500,
    status: 'Pending',
    categoryId: 'c-moradia',
    categoryName: 'Moradia',
    parentCategoryName: null,
    categoryColor: '#2563eb',
    categoryIcon: 'pi-home',
    accountId: null,
    accountName: null,
    paymentMethod: null,
    transactionDate: '2026-11-05',
    dueDate: '2026-11-10',
    paidAt: null,
    notes: null,
    createdAt: '2026-11-05T10:00:00Z',
    updatedAt: '2026-11-05T10:00:00Z',
    ...partial,
  }
}

export function makeDetail(partial: Partial<TransactionDetailDto> = {}): TransactionDetailDto {
  return { ...makeTransaction(), payments: [], ...partial }
}

export function makeCategory(partial: Partial<CategoryDto> = {}): CategoryDto {
  return {
    id: 'c-moradia',
    name: 'Moradia',
    type: 'Expense',
    parentCategoryId: null,
    icon: 'pi-home',
    color: '#2563eb',
    isDefault: true,
    isActive: true,
    ...partial,
  }
}

export function makeAccount(partial: Partial<AccountDto> = {}): AccountDto {
  return {
    id: 'a1',
    name: 'Nubank',
    institution: 'Nu Pagamentos',
    type: 'Checking',
    currency: 'BRL',
    isActive: true,
    latestSnapshot: { balance: 1000, referenceDate: '2026-11-01' },
    projectedBalance: 850.5,
    projectedAsOf: '2026-11-18',
    createdAt: '2026-11-01T10:00:00Z',
    updatedAt: '2026-11-01T10:00:00Z',
    ...partial,
  }
}

export function makeDashboard(partial: Partial<DashboardDto> = {}): DashboardDto {
  return {
    period: { from: '2026-11-01', to: '2026-11-30' },
    balance: {
      reportedTotal: 1000,
      projectedTotal: 850.5,
      projectedAsOf: '2026-11-30',
      accountsWithBalance: 1,
      accountsWithoutBalance: 0,
    },
    totals: {
      income: 5000,
      expense: 2000,
      result: 3000,
      receivedIncome: 5000,
      pendingIncome: 0,
      paidExpense: 500,
      pendingExpense: 1500,
    },
    expensesByCategory: [
      { categoryId: 'c1', categoryName: 'Moradia', color: '#2563eb', total: 1500, percentage: 75 },
      { categoryId: 'c2', categoryName: 'Lazer', color: null, total: 500, percentage: 25 },
    ],
    upcomingDues: [
      makeTransaction({
        id: 'late',
        description: 'Internet',
        status: 'Overdue',
        dueDate: '2026-11-15',
        remainingAmount: 120,
        amount: 120,
      }),
      makeTransaction({ id: 'soon', description: 'Condomínio', dueDate: '2026-11-20' }),
    ],
    monthlyHistory: [
      { month: '2026-06', income: 0, expense: 0, result: 0 },
      { month: '2026-11', income: 5000, expense: 2000, result: 3000 },
    ],
    today: '2026-11-18',
    ...partial,
  }
}
