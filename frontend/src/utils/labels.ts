import type {
  AccountType,
  CategoryType,
  InstallmentPlanStatus,
  PaymentMethod,
  RecurrenceFrequency,
  TransactionHistoryAction,
  TransactionStatus,
  TransactionType,
} from '@/types/api'

export const TRANSACTION_TYPE_LABELS: Record<TransactionType, string> = {
  Expense: 'Despesa',
  Income: 'Receita',
}

export const CATEGORY_TYPE_LABELS: Record<CategoryType, string> = {
  Expense: 'Despesa',
  Income: 'Receita',
}

export const ACCOUNT_TYPE_LABELS: Record<AccountType, string> = {
  Checking: 'Conta corrente',
  Savings: 'Poupança',
  Wallet: 'Carteira',
  Investment: 'Investimentos',
  Other: 'Outra',
}

export const PAYMENT_METHOD_LABELS: Record<PaymentMethod, string> = {
  Cash: 'Dinheiro',
  DebitCard: 'Cartão de débito',
  CreditCard: 'Cartão de crédito',
  Pix: 'Pix',
  BankTransfer: 'Transferência',
  BankSlip: 'Boleto',
  Other: 'Outro',
}

export const HISTORY_ACTION_LABELS: Record<TransactionHistoryAction, string> = {
  Created: 'Movimentação criada',
  Updated: 'Movimentação alterada',
  PaymentAdded: 'Pagamento registrado',
  PaymentUpdated: 'Pagamento alterado',
  PaymentRemoved: 'Pagamento removido',
  Canceled: 'Movimentação cancelada',
  Reopened: 'Movimentação reaberta',
}

export interface StatusPresentation {
  label: string
  icon: string
  /** Sufixo de classe CSS (cor + texto + ícone: a cor nunca é o único indicador). */
  tone: 'neutral' | 'info' | 'warning' | 'success' | 'danger'
}

export const STATUS_PRESENTATION: Record<TransactionStatus, StatusPresentation> = {
  Planned: { label: 'Previsto', icon: 'pi pi-calendar', tone: 'info' },
  Pending: { label: 'Pendente', icon: 'pi pi-clock', tone: 'warning' },
  Paid: { label: 'Pago', icon: 'pi pi-check-circle', tone: 'success' },
  Received: { label: 'Recebido', icon: 'pi pi-check-circle', tone: 'success' },
  Overdue: { label: 'Atrasado', icon: 'pi pi-exclamation-circle', tone: 'danger' },
  Canceled: { label: 'Cancelado', icon: 'pi pi-ban', tone: 'neutral' },
}

export const TRANSACTION_STATUSES = Object.keys(STATUS_PRESENTATION) as TransactionStatus[]
export const TRANSACTION_TYPES = Object.keys(TRANSACTION_TYPE_LABELS) as TransactionType[]
export const PAYMENT_METHODS = Object.keys(PAYMENT_METHOD_LABELS) as PaymentMethod[]
export const ACCOUNT_TYPES = Object.keys(ACCOUNT_TYPE_LABELS) as AccountType[]

/** Rótulo do botão de pagamento conforme o tipo da movimentação. */
export function paymentActionLabel(type: TransactionType): string {
  return type === 'Income' ? 'Registrar recebimento' : 'Registrar pagamento'
}

export function enumOptions<T extends string>(labels: Record<T, string>): { value: T; label: string }[] {
  return (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }))
}

/** Rótulo do método de pagamento; `null` (ex.: relatórios) = "Não informado". */
export function paymentMethodLabel(method: PaymentMethod | null | undefined): string {
  return method ? PAYMENT_METHOD_LABELS[method] : 'Não informado'
}

export const INSTALLMENT_STATUS_PRESENTATION: Record<InstallmentPlanStatus, StatusPresentation> = {
  Open: { label: 'Em aberto', icon: 'pi pi-clock', tone: 'warning' },
  Completed: { label: 'Concluído', icon: 'pi pi-check-circle', tone: 'success' },
  Canceled: { label: 'Cancelado', icon: 'pi pi-ban', tone: 'neutral' },
}

export const INSTALLMENT_STATUSES = Object.keys(INSTALLMENT_STATUS_PRESENTATION) as InstallmentPlanStatus[]

/** Opções do seletor de frequência (a regra de passo é do backend — D-027). */
export const FREQUENCY_OPTION_LABELS: Record<RecurrenceFrequency, string> = {
  Daily: 'Diária',
  Weekly: 'Semanal',
  Biweekly: 'Quinzenal',
  Monthly: 'Mensal',
  Bimonthly: 'Bimestral',
  Quarterly: 'Trimestral',
  Semiannual: 'Semestral',
  Annual: 'Anual',
  Custom: 'Personalizada (a cada N dias)',
}

export const RECURRENCE_FREQUENCIES = Object.keys(FREQUENCY_OPTION_LABELS) as RecurrenceFrequency[]
