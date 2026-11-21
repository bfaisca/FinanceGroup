import type {
  GroupDashboardDto,
  GroupDetailDto,
  GroupInvitationDto,
  GroupMemberDto,
  GroupPaidByItemDto,
  GroupSummaryDto,
  GroupTransactionDetailDto,
  GroupTransactionDto,
  GroupTransactionPaymentDto,
  InvitationPreviewDto,
} from '../types'

/** Fixtures usadas SOMENTE em testes (nunca importar em código de produção). */
export function makeGroupSummary(partial: Partial<GroupSummaryDto> = {}): GroupSummaryDto {
  return {
    id: 'g1',
    name: 'Casa',
    description: 'Contas da casa',
    myRole: 'Member',
    memberCount: 3,
    isActive: true,
    createdAt: '2026-11-01T10:00:00Z',
    ...partial,
  }
}

export function makeGroupMember(partial: Partial<GroupMemberDto> = {}): GroupMemberDto {
  return {
    id: 'm1',
    userId: 'u1',
    name: 'Maria Silva',
    role: 'Member',
    status: 'Active',
    joinedAt: '2026-11-01T10:00:00Z',
    leftAt: null,
    isMe: false,
    ...partial,
  }
}

export function makeGroupDetail(partial: Partial<GroupDetailDto> = {}): GroupDetailDto {
  return {
    ...makeGroupSummary({ myRole: 'Owner' }),
    createdByName: 'Maria Silva',
    updatedAt: '2026-11-01T10:00:00Z',
    permissions: {
      canManageMembers: true,
      canInvite: true,
      canEditGroup: true,
      canArchive: true,
      canLeave: false,
      canCreateTransactions: true,
    },
    members: [
      makeGroupMember({ id: 'm1', userId: 'u1', name: 'Maria Silva', role: 'Owner', isMe: true }),
      makeGroupMember({ id: 'm2', userId: 'u2', name: 'João Souza', role: 'Admin' }),
      makeGroupMember({ id: 'm3', userId: 'u3', name: 'Ana Lima', role: 'Member' }),
    ],
    ...partial,
  }
}

/** Detalhe do ponto de vista de um Member comum. */
export function makeMemberViewDetail(partial: Partial<GroupDetailDto> = {}): GroupDetailDto {
  return makeGroupDetail({
    myRole: 'Member',
    permissions: {
      canManageMembers: false,
      canInvite: false,
      canEditGroup: false,
      canArchive: false,
      canLeave: true,
      canCreateTransactions: true,
    },
    members: [
      makeGroupMember({ id: 'm1', userId: 'u1', name: 'Maria Silva', role: 'Owner' }),
      makeGroupMember({ id: 'm3', userId: 'u3', name: 'Ana Lima', role: 'Member', isMe: true }),
    ],
    ...partial,
  })
}

export function makeInvitation(partial: Partial<GroupInvitationDto> = {}): GroupInvitationDto {
  return {
    id: 'i1',
    email: 'convidado@exemplo.com',
    status: 'Pending',
    invitedByName: 'Maria Silva',
    expiresAt: '2026-11-27T10:00:00Z',
    createdAt: '2026-11-20T10:00:00Z',
    ...partial,
  }
}

export function makePreview(partial: Partial<InvitationPreviewDto> = {}): InvitationPreviewDto {
  return {
    groupName: 'Casa',
    invitedByName: 'Maria Silva',
    email: 'ana@exemplo.com',
    status: 'Pending',
    expiresAt: '2026-11-27T10:00:00Z',
    canAccept: true,
    alreadyMember: false,
    ...partial,
  }
}

export function makeGroupTransaction(partial: Partial<GroupTransactionDto> = {}): GroupTransactionDto {
  return {
    id: 'gt1',
    groupId: 'g1',
    description: 'Mercado',
    type: 'Expense',
    amount: 200,
    paidAmount: 0,
    interestPaid: 0,
    totalPaid: 0,
    remainingAmount: 200,
    status: 'Pending',
    categoryId: 'c-alimentacao',
    categoryName: 'Alimentação',
    parentCategoryName: null,
    categoryColor: '#16a34a',
    categoryIcon: 'pi-shopping-cart',
    paymentMethod: null,
    transactionDate: '2026-11-05',
    dueDate: '2026-11-10',
    paidAt: null,
    notes: null,
    series: null,
    createdBy: { userId: 'u1', name: 'Maria Silva' },
    createdAt: '2026-11-05T10:00:00Z',
    updatedAt: '2026-11-05T10:00:00Z',
    permissions: { canEdit: true, canCancel: true, canDelete: true, canPay: true },
    ...partial,
  }
}

export function makeGroupPayment(partial: Partial<GroupTransactionPaymentDto> = {}): GroupTransactionPaymentDto {
  return {
    id: 'gp1',
    amount: 50,
    interestAmount: 0,
    totalAmount: 50,
    paymentDate: '2026-11-10',
    paymentMethod: 'Pix',
    paidBy: { userId: 'u2', name: 'João Souza' },
    createdAt: '2026-11-10T10:00:00Z',
    updatedAt: '2026-11-10T10:00:00Z',
    canEdit: false,
    canRemove: false,
    ...partial,
  }
}

export function makeGroupTransactionDetail(
  partial: Partial<GroupTransactionDetailDto> = {},
): GroupTransactionDetailDto {
  return { ...makeGroupTransaction(), payments: [], ...partial }
}

export function makePaidBy(partial: Partial<GroupPaidByItemDto> = {}): GroupPaidByItemDto {
  return {
    userId: 'u2',
    name: 'João Souza',
    status: 'Active',
    paidAmount: 423.5,
    interestAmount: 23.5,
    percent: 100,
    ...partial,
  }
}

export function makeGroupDashboard(partial: Partial<GroupDashboardDto> = {}): GroupDashboardDto {
  return {
    period: { from: '2026-11-01', to: '2026-11-30' },
    totals: {
      expenses: 1000,
      expensesPaidPrincipal: 400,
      interestPaid: 23.5,
      expensesPaid: 423.5,
      expensesPending: 600,
    },
    paidBy: [makePaidBy()],
    expensesByCategory: [
      {
        categoryId: 'c1',
        name: 'Moradia',
        color: '#2563eb',
        amount: 800,
        percent: 80,
        children: [{ categoryId: 'c1a', name: 'Condomínio', amount: 300, percent: 37.5 }],
      },
      { categoryId: 'c2', name: 'Lazer', color: null, amount: 200, percent: 20, children: [] },
    ],
    monthly: [
      { month: '2026-10', expenses: 0, paid: 0 },
      { month: '2026-11', expenses: 1000, paid: 423.5 },
    ],
    previousPeriod: {
      from: '2026-10-01',
      to: '2026-10-31',
      expenses: 800,
      expensesChangePercent: 25,
    },
    ...partial,
  }
}
