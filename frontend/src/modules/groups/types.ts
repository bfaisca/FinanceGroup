/**
 * Tipos espelhando docs/API_CONTRACTS.md §16 (Etapa 4 — Grupos). Não adicionar campos fora do contrato.
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
  TransactionType,
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
  type: TransactionType
  amount: number
  paidAmount: number
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
  createdBy: GroupUserRefDto
  createdAt: IsoDateTimeString
  updatedAt: IsoDateTimeString
  permissions: GroupTransactionPermissions
}

export interface GroupTransactionPaymentDto {
  id: Uuid
  amount: number
  paymentDate: DateOnlyString
  paymentMethod: PaymentMethod | null
  paidBy: GroupUserRefDto
  createdAt: IsoDateTimeString
  /** Só o próprio pagador, e com o grupo ativo. */
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

export interface CreateGroupTransactionRequest {
  description: string
  type: TransactionType
  amount: number
  categoryId: Uuid
  paymentMethod?: PaymentMethod | null
  /** Padrão `Pending`. */
  status?: TransactionInitialStatus
  transactionDate: DateOnlyString
  dueDate?: DateOnlyString | null
  notes?: string | null
}

/** Mesmos campos, exceto `type` (imutável). */
export type UpdateGroupTransactionRequest = Omit<CreateGroupTransactionRequest, 'type'>

/** `accountId` = conta do próprio pagador; é privada e nunca devolvida. */
export interface CreateGroupPaymentRequest {
  amount: number
  paymentDate: DateOnlyString
  accountId?: Uuid | null
  paymentMethod?: PaymentMethod | null
}

export type GroupTransactionSortField = 'transactionDate' | 'dueDate' | 'amount' | 'description' | 'createdAt'

/** Query de `GET /groups/{id}/transactions`. */
export interface GroupTransactionListQuery extends PageQuery {
  from?: DateOnlyString
  to?: DateOnlyString
  type?: TransactionType
  /** Inclui subcategorias. */
  categoryId?: Uuid
  status?: TransactionStatus
  search?: string
  createdByUserId?: Uuid
  /** Tem ao menos um pagamento dele. */
  paidByUserId?: Uuid
}

// ---------- 16.3 Dashboard do grupo ----------

export interface GroupParticipationItemDto {
  userId: Uuid
  name: string
  status: GroupMemberStatus
  expenseAmount: number
  /** 0–100, 2 casas. */
  expensePercent: number
  incomeAmount: number
  incomePercent: number
}

export interface GroupDashboardDto {
  period: { from: DateOnlyString; to: DateOnlyString }
  totals: {
    expenses: number
    expensesPaid: number
    expensesPending: number
    income: number
    incomeReceived: number
    incomePending: number
    /** income - expenses */
    balance: number
  }
  /** Por quem CADASTROU (soma dos valores). */
  createdBy: GroupParticipationItemDto[]
  /** Por quem PAGOU/RECEBEU (soma dos pagamentos). */
  paidBy: GroupParticipationItemDto[]
  expensesByCategory: {
    categoryId: Uuid
    name: string
    color: string | null
    amount: number
    percent: number
    children: { categoryId: Uuid; name: string; amount: number; percent: number }[]
  }[]
  /** `month` = YYYY-MM */
  monthly: { month: string; income: number; expenses: number }[]
  previousPeriod: {
    from: DateOnlyString
    to: DateOnlyString
    expenses: number
    income: number
    /** `null` se a base é 0. */
    expensesChangePercent: number | null
    incomeChangePercent: number | null
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
