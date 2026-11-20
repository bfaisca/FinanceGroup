import type {
  AccountDto,
  CategoryDto,
  CreditCardDto,
  DashboardDto,
  InstallmentPlanDetailDto,
  InstallmentPlanDto,
  RecurrenceDto,
  TransactionDetailDto,
  TransactionDto,
} from '@/types/api'

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
    creditCardId: null,
    creditCardName: null,
    installment: null,
    recurrenceRuleId: null,
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
    creditCards: [],
    ...partial,
  }
}

export function makeCreditCard(partial: Partial<CreditCardDto> = {}): CreditCardDto {
  return {
    id: 'cc1',
    name: 'Nubank Roxinho',
    institution: 'Nu Pagamentos',
    creditLimit: 5000,
    closingDay: 5,
    dueDay: 12,
    accountId: 'a1',
    accountName: 'Nubank',
    isActive: true,
    usedLimit: 1250,
    availableLimit: 3750,
    openInvoice: { closingDate: '2026-12-05', dueDate: '2026-12-12', amount: 480.9 },
    createdAt: '2026-11-01T10:00:00Z',
    updatedAt: '2026-11-01T10:00:00Z',
    ...partial,
  }
}

export function makePlan(partial: Partial<InstallmentPlanDto> = {}): InstallmentPlanDto {
  return {
    id: 'p1',
    description: 'Notebook',
    totalAmount: 3000,
    installmentCount: 12,
    firstDate: '2026-10-10',
    categoryId: 'c-compras',
    categoryName: 'Compras',
    accountId: null,
    accountName: null,
    creditCardId: 'cc1',
    creditCardName: 'Nubank Roxinho',
    paymentMethod: 'CreditCard',
    status: 'Open',
    paidCount: 2,
    openCount: 10,
    canceledCount: 0,
    paidAmount: 500,
    remainingAmount: 2500,
    nextDueDate: '2026-12-12',
    createdAt: '2026-10-10T10:00:00Z',
    ...partial,
  }
}

export function makePlanDetail(partial: Partial<InstallmentPlanDetailDto> = {}): InstallmentPlanDetailDto {
  return {
    ...makePlan(),
    installments: [
      makeTransaction({
        id: 'i1',
        description: 'Notebook',
        amount: 250,
        paidAmount: 250,
        remainingAmount: 0,
        status: 'Paid',
        transactionDate: '2026-10-10',
        dueDate: '2026-11-12',
        creditCardId: 'cc1',
        creditCardName: 'Nubank Roxinho',
        installment: { planId: 'p1', number: 1, count: 12 },
      }),
      makeTransaction({
        id: 'i2',
        description: 'Notebook',
        amount: 250,
        remainingAmount: 250,
        status: 'Pending',
        transactionDate: '2026-11-10',
        dueDate: '2026-12-12',
        creditCardId: 'cc1',
        creditCardName: 'Nubank Roxinho',
        installment: { planId: 'p1', number: 2, count: 12 },
      }),
    ],
    ...partial,
  }
}

export function makeRecurrence(partial: Partial<RecurrenceDto> = {}): RecurrenceDto {
  return {
    id: 'r1',
    description: 'Netflix',
    type: 'Expense',
    amount: 55.9,
    categoryId: 'c-lazer',
    categoryName: 'Lazer',
    accountId: null,
    accountName: null,
    creditCardId: 'cc1',
    creditCardName: 'Nubank Roxinho',
    paymentMethod: 'CreditCard',
    notes: null,
    frequency: 'Monthly',
    interval: 1,
    startDate: '2026-01-10',
    endDate: null,
    isActive: true,
    nextOccurrenceDate: '2026-12-10',
    generatedCount: 12,
    createdAt: '2026-01-01T10:00:00Z',
    updatedAt: '2026-01-01T10:00:00Z',
    ...partial,
  }
}
