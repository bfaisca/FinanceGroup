/**
 * Tipos espelhando docs/API_CONTRACTS.md §16 (Etapa 4 — Grupos; v1.5: somente despesas). Não adicionar campos fora do contrato.
 */
import type {
  DateOnlyString,
  GroupInvitationStatus,
  GroupMemberRole,
  GroupMemberStatus,
  IsoDateTimeString,
  PageQuery,
  PaymentMethod,
  TransactionHistoryAction,
  TransactionInitialStatus,
  TransactionStatus,
  Uuid,
} from '@/types/api'

// ---------- 16. Grupos e membros ----------

export interface GroupSummaryDto {
  id: Uuid
  name: string
  description: string | null
  myRole: GroupMemberRole
  /** Membros ativos. */
  memberCount: number
  /** `false` = arquivado. */
  isActive: boolean
  createdAt: IsoDateTimeString
}

export interface GroupMemberDto {
  /** Id do GroupMember. */
  id: Uuid
  userId: Uuid
  /** "Usuário removido" se a conta foi excluída. */
  name: string
  role: GroupMemberRole
  status: GroupMemberStatus
  joinedAt: IsoDateTimeString
  leftAt: IsoDateTimeString | null
  isMe: boolean
}

export interface GroupPermissions {
  canManageMembers: boolean
  canInvite: boolean
  canEditGroup: boolean
  canArchive: boolean
  canLeave: boolean
  canCreateTransactions: boolean
}

export interface GroupDetailDto extends GroupSummaryDto {
  createdByName: string
  updatedAt: IsoDateTimeString
  permissions: GroupPermissions
  /** Apenas ativos. */
  members: GroupMemberDto[]
}

export interface GroupRequest {
  /** 1–100 */
  name: string
  /** ≤ 500 */
  description?: string | null
}

export type AssignableRole = Exclude<GroupMemberRole, 'Owner'>

// ---------- 16.1 Convites ----------

export interface GroupInvitationDto {
  id: Uuid
  email: string
  /** `Expired` é derivado. */
  status: GroupInvitationStatus
  invitedByName: string
  expiresAt: IsoDateTimeString
  createdAt: IsoDateTimeString
  /** Apenas na resposta de POST/resend. */
  emailSent?: boolean
}

export interface InvitationPreviewDto {
  groupName: string
  invitedByName: string
  email: string
  status: GroupInvitationStatus
  expiresAt: IsoDateTimeString
  /** Pending + não expirado + e-mail do usuário logado = e-mail do convite. */
  canAccept: boolean
  alreadyMember: boolean
}

// ---------- 16.2 Movimentações do grupo ----------

/** "Usuário removido" quando anonimizado. */
export interface GroupUserRefDto {
  userId: Uuid
  name: string
}

export interface GroupTransactionPermissions {
  canEdit: boolean
  canCancel: boolean
  canDelete: boolean
  canPay: boolean
}

export interface GroupTransactionDto {
  id: Uuid
  groupId: Uuid
  description: string
  /** Grupos só têm despesas (D-046). */
  type: 'Expense'
  /** Valor ORIGINAL da conta (editável por ocorrência). */
  amount: number
  /** Principal já coberto (Σ dos pagamentos); só ele define a situação. */
  paidAmount: number
  /** Σ juros/multa pagos (D-048). */
  interestPaid: number
  /** `paidAmount + interestPaid` = efetivamente pago. */
  totalPaid: number
  /** `amount − paidAmount`. */
  remainingAmount: number
  /** Apresentado (inclui `Overdue`). */
  status: TransactionStatus
  categoryId: Uuid
  categoryName: string
  parentCategoryName: string | null
  categoryColor: string | null
  categoryIcon: string | null
  paymentMethod: PaymentMethod | null
  transactionDate: DateOnlyString
  dueDate: DateOnlyString | null
  paidAt: DateOnlyString | null
  notes: string | null
  /** Recorrência do grupo (D-049); `null` se não faz parte de uma série. */
  series: GroupSeriesDto | null
  createdBy: GroupUserRefDto
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
  permissions: GroupTransactionPermissions
}

export interface GroupSeriesDto {
  id: Uuid
  /** Posição da ocorrência (1-based). */
  number: number
  total: number
}

export interface GroupTransactionPaymentDto {
  id: Uuid
  /** Principal aplicado à conta. */
  amount: number
  /** Juros/multa (≥ 0). */
  interestAmount: number
  /** `amount + interestAmount`. */
  totalAmount: number
  paymentDate: DateOnlyString
  paymentMethod: PaymentMethod | null
  paidBy: GroupUserRefDto
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
  /** Só o próprio pagador, com o grupo ativo e a conta não cancelada. */
  canEdit: boolean
  canRemove: boolean
}

export interface GroupTransactionDetailDto extends GroupTransactionDto {
  payments: GroupTransactionPaymentDto[]
}

export interface GroupTransactionHistoryEntryDto {
  id: Uuid
  action: TransactionHistoryAction
  field: string | null
  oldValue: string | null
  newValue: string | null
  actorName: string
  createdAt: IsoDateTimeString
}

/** `accountId` = conta do próprio pagador; é privada e nunca devolvida. */
export interface GroupPaymentRequest {
  /** Principal aplicado (> 0); Σ dos pagamentos ≤ valor da conta. */
  amount: number
  /** Juros/multa (≥ 0, padrão 0); não entra na regra "≤ valor da conta". */
  interestAmount?: number
  /** ≤ hoje; qualquer data passada. */
  paymentDate: DateOnlyString
  accountId?: Uuid | null
  /** Só na EDIÇÃO (PUT): sem `accountId` a conta privada atual é mantida; `true` desvincula. */
  clearAccount?: boolean
  paymentMethod?: PaymentMethod | null
}

export type GroupRecurrenceFrequency =
  'Weekly' | 'Biweekly' | 'Monthly' | 'Bimonthly' | 'Quarterly' | 'Semiannual' | 'Annual'

export interface GroupRecurrenceRequest {
  frequency: GroupRecurrenceFrequency
  /** 2–60. */
  occurrences: number
}

/** Sem `type` (sempre despesa). */
export interface CreateGroupTransactionRequest {
  description: string
  amount: number
  categoryId: Uuid
  paymentMethod?: PaymentMethod | null
  /** Padrão `Pending`. */
  status?: TransactionInitialStatus
  transactionDate: DateOnlyString
  /** Livre: pode ser anterior a hoje e à data da movimentação. */
  dueDate?: DateOnlyString | null
  notes?: string | null
  /** "Já paga": cria a conta e o 1º pagamento do criador (D-047). */
  payment?: GroupPaymentRequest | null
  /** N ocorrências geradas na hora (D-049). */
  recurrence?: GroupRecurrenceRequest | null
}

/** Edita só ESTA ocorrência; sem `payment` nem `recurrence`. */
export type UpdateGroupTransactionRequest = Omit<CreateGroupTransactionRequest, 'payment' | 'recurrence'>

export type GroupTransactionSortField = 'transactionDate' | 'dueDate' | 'amount' | 'description' | 'createdAt'

/** Query de `GET /groups/{id}/transactions`. */
export interface GroupTransactionListQuery extends PageQuery {
  from?: DateOnlyString
  to?: DateOnlyString
  /** Inclui subcategorias. */
  categoryId?: Uuid
  status?: TransactionStatus
  search?: string
  createdByUserId?: Uuid
  /** Tem ao menos um pagamento dele. */
  paidByUserId?: Uuid
}

// ---------- 16.3 Dashboard do grupo ----------

export interface GroupPaidByItemDto {
  userId: Uuid
  name: string
  status: GroupMemberStatus
  /** Σ (principal + juros) dos pagamentos dessa pessoa. */
  paidAmount: number
  /** Parte de juros/multa dentro de `paidAmount`. */
  interestAmount: number
  /** 0–100, 2 casas, sobre `totals.expensesPaid`. */
  percent: number
}

export interface GroupDashboardDto {
  period: { from: DateOnlyString; to: DateOnlyString }
  totals: {
    /** Σ valores originais. */
    expenses: number
    expensesPaidPrincipal: number
    /** Σ juros/multa. */
    interestPaid: number
    /** `expensesPaidPrincipal + interestPaid` (efetivamente pago). */
    expensesPaid: number
    /** `expenses − expensesPaidPrincipal` (só principal; nunca negativo). */
    expensesPending: number
  }
  /** "Contas Pagas": quem pagou (só quem pagou algo). */
  paidBy: GroupPaidByItemDto[]
  expensesByCategory: {
    categoryId: Uuid
    name: string
    color: string | null
    amount: number
    percent: number
    children: { categoryId: Uuid; name: string; amount: number; percent: number }[]
  }[]
  /** `month` = YYYY-MM; `paid` = efetivamente pago das contas do mês. */
  monthly: { month: string; expenses: number; paid: number }[]
  previousPeriod: {
    from: DateOnlyString
    to: DateOnlyString
    expenses: number
    /** `null` se a base é 0. */
    expensesChangePercent: number | null
  }
}

export interface GroupDashboardQuery {
  from: DateOnlyString
  to: DateOnlyString
  /** 1–36 (padrão 6). */
  months?: number
  /** `YYYY-MM` (padrão: mês de `to`). */
  endMonth?: string
}
