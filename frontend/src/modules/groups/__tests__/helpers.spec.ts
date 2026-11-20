import { describe, expect, it } from 'vitest'
import { groupTransactionActions } from '../actions'
import {
  displayName,
  formatChange,
  isGroupDashboardEmpty,
  NOT_DEBT_NOTICE,
  PARTICIPATION_COPY,
  participationRows,
} from '../dashboard'
import {
  countActiveGroupFilters,
  mergeGroupRouteQuery,
  parseGroupTransactionFilters,
  toGroupListQuery,
  toGroupRouteQuery,
} from '../filters'
import { invitationState } from '../invitation'
import { memberActions } from '../members'
import {
  buildGroupCreateRequest,
  buildGroupRequest,
  buildGroupUpdateRequest,
  validateGroupForm,
  validateGroupTransactionForm,
  type GroupTransactionFormValues,
} from '../validation'
import { makeGroupDashboard, makeGroupMember, makeGroupTransaction, makeParticipation } from './fixtures'

describe('memberActions (D-030)', () => {
  const member = makeGroupMember({ role: 'Member' })
  const admin = makeGroupMember({ role: 'Admin' })

  it('Owner promove Members, rebaixa Admins e remove ambos', () => {
    expect(memberActions('Owner', true, member)).toEqual({ canPromote: true, canDemote: false, canRemove: true })
    expect(memberActions('Owner', true, admin)).toEqual({ canPromote: false, canDemote: true, canRemove: true })
  })

  it('Admin promove e remove Members, mas não mexe em outro Admin', () => {
    expect(memberActions('Admin', true, member)).toEqual({ canPromote: true, canDemote: false, canRemove: true })
    expect(memberActions('Admin', true, admin)).toEqual({ canPromote: false, canDemote: false, canRemove: false })
  })

  it('Member comum, o próprio usuário, o Owner e ex-membros não têm ações', () => {
    const none = { canPromote: false, canDemote: false, canRemove: false }
    expect(memberActions('Member', false, member)).toEqual(none)
    expect(memberActions('Owner', true, makeGroupMember({ isMe: true }))).toEqual(none)
    expect(memberActions('Admin', true, makeGroupMember({ role: 'Owner' }))).toEqual(none)
    expect(memberActions('Owner', true, makeGroupMember({ status: 'Left' }))).toEqual(none)
  })
})

describe('groupTransactionActions', () => {
  it('segue as permissões do backend', () => {
    const own = makeGroupTransaction()
    expect(groupTransactionActions(own)).toMatchObject({
      canEdit: true,
      canPay: true,
      canCancel: true,
      canDelete: true,
      canReopen: false,
    })
    const other = makeGroupTransaction({
      permissions: { canEdit: false, canCancel: false, canDelete: false, canPay: true },
    })
    expect(groupTransactionActions(other)).toMatchObject({
      canEdit: false,
      canCancel: false,
      canDelete: false,
      canPay: true,
    })
  })

  it('cancelada: só reabre (para o criador) e não paga', () => {
    const canceled = makeGroupTransaction({ status: 'Canceled' })
    expect(groupTransactionActions(canceled)).toMatchObject({
      canReopen: true,
      canCancel: false,
      canEdit: false,
      canPay: false,
    })
    const foreign = makeGroupTransaction({
      status: 'Canceled',
      permissions: { canEdit: false, canCancel: false, canDelete: false, canPay: false },
    })
    expect(groupTransactionActions(foreign).canReopen).toBe(false)
  })

  it('com pagamentos explica por que não exclui', () => {
    const tx = makeGroupTransaction({
      paidAmount: 50,
      remainingAmount: 150,
      permissions: { canEdit: true, canCancel: true, canDelete: false, canPay: true },
    })
    expect(groupTransactionActions(tx).deleteBlockedByPayments).toBe(true)
  })

  it('não oferece pagar quando nada resta, mesmo que o backend permita', () => {
    expect(groupTransactionActions(makeGroupTransaction({ remainingAmount: 0, paidAmount: 200 })).canPay).toBe(false)
  })
})

describe('invitationState', () => {
  const base = { status: 'Pending' as const, canAccept: true, alreadyMember: false }
  it('mapeia cada estado do convite', () => {
    expect(invitationState(base)).toBe('actionable')
    expect(invitationState({ ...base, canAccept: false })).toBe('wrongEmail')
    expect(invitationState({ ...base, alreadyMember: true })).toBe('alreadyMember')
    expect(invitationState({ ...base, status: 'Expired', canAccept: false })).toBe('expired')
    expect(invitationState({ ...base, status: 'Revoked', canAccept: false })).toBe('revoked')
    expect(invitationState({ ...base, status: 'Accepted', canAccept: false })).toBe('accepted')
    expect(invitationState({ ...base, status: 'Declined', canAccept: false })).toBe('declined')
  })
})

describe('validação de grupo', () => {
  it('nome obrigatório (1–100) e descrição até 500', () => {
    expect(validateGroupForm({ name: '  ', description: '' }).name).toBe('Informe o nome do grupo.')
    expect(validateGroupForm({ name: 'a'.repeat(101), description: '' }).name).toContain('100')
    expect(validateGroupForm({ name: 'Casa', description: 'x'.repeat(501) }).description).toContain('500')
    expect(validateGroupForm({ name: 'Casa', description: '' })).toEqual({})
  })

  it('monta a requisição com trim e descrição vazia como null', () => {
    expect(buildGroupRequest({ name: '  Casa ', description: '  ' })).toEqual({ name: 'Casa', description: null })
    expect(buildGroupRequest({ name: 'Casa', description: ' Contas ' })).toEqual({
      name: 'Casa',
      description: 'Contas',
    })
  })
})

describe('validação da movimentação do grupo', () => {
  const valid: GroupTransactionFormValues = {
    description: 'Mercado',
    type: 'Expense',
    amount: '1.234,50',
    categoryId: 'c1',
    paymentMethod: null,
    status: 'Pending',
    transactionDate: '2026-11-05',
    dueDate: null,
    notes: '',
  }

  it('aceita valores válidos e monta a requisição SEM conta nem cartão', () => {
    expect(validateGroupTransactionForm(valid)).toEqual({})
    const request = buildGroupCreateRequest(valid)
    expect(request).toEqual({
      description: 'Mercado',
      type: 'Expense',
      amount: 1234.5,
      categoryId: 'c1',
      paymentMethod: null,
      status: 'Pending',
      transactionDate: '2026-11-05',
      dueDate: null,
      notes: null,
    })
    expect(Object.keys(request)).not.toContain('accountId')
    expect(Object.keys(request)).not.toContain('creditCardId')
  })

  it('atualização não envia `type` e só envia `status` quando escolhido', () => {
    const update = buildGroupUpdateRequest({ ...valid, status: null })
    expect(update).not.toHaveProperty('type')
    expect(update).not.toHaveProperty('status')
  })

  it('valida descrição, valor, categoria, datas e observações', () => {
    const errors = validateGroupTransactionForm({
      ...valid,
      description: '',
      amount: '0',
      categoryId: null,
      transactionDate: '2026-11-10',
      dueDate: '2026-11-01',
      notes: 'x'.repeat(1001),
    })
    expect(Object.keys(errors).sort()).toEqual(['amount', 'categoryId', 'description', 'dueDate', 'notes'])
  })

  it('impede reduzir o valor abaixo do já pago', () => {
    const errors = validateGroupTransactionForm({ ...valid, amount: '10,00' }, { minAmountCents: 5000 })
    expect(errors.amount).toContain('já pago')
  })
})

describe('filtros da URL', () => {
  it('lê filtros válidos, ignora inválidos e usa os nomes createdBy/paidBy', () => {
    const filters = parseGroupTransactionFilters({
      from: '2026-11-01',
      to: 'lixo',
      type: 'Income',
      status: 'Overdue',
      categoryId: 'c1',
      createdBy: 'u1',
      paidBy: 'u2',
      search: ' luz ',
      page: '3',
      pageSize: '500',
      sort: '-campoInvalido',
    })
    expect(filters).toMatchObject({
      from: '2026-11-01',
      type: 'Income',
      status: 'Overdue',
      categoryId: 'c1',
      createdByUserId: 'u1',
      paidByUserId: 'u2',
      search: 'luz',
      page: 3,
      pageSize: 100,
      sort: '-transactionDate',
    })
    expect(filters.to).toBeUndefined()
  })

  it('serializa para a URL (omitindo padrões) e para a API (nomes do contrato)', () => {
    const filters = parseGroupTransactionFilters({ createdBy: 'u1', paidBy: 'u2', page: '2' })
    expect(toGroupRouteQuery(filters)).toEqual({ createdBy: 'u1', paidBy: 'u2', page: '2' })
    expect(toGroupListQuery(filters)).toMatchObject({ createdByUserId: 'u1', paidByUserId: 'u2', page: 2 })
    expect(countActiveGroupFilters(filters)).toBe(2)
  })

  it('preserva tab/open/new ao mesclar e descarta outros parâmetros alheios', () => {
    const merged = mergeGroupRouteQuery(
      { tab: 'transactions', open: 'gt1', foo: 'bar' },
      parseGroupTransactionFilters({ search: 'x' }),
    )
    expect(merged).toEqual({ tab: 'transactions', open: 'gt1', search: 'x' })
  })
})

describe('dashboard helpers', () => {
  it('marca ex-membros com "(ex-membro)"', () => {
    expect(displayName({ name: 'Ana', status: 'Active' })).toBe('Ana')
    expect(displayName({ name: 'Carlos', status: 'Left' })).toBe('Carlos (ex-membro)')
    expect(displayName({ name: 'Usuário removido', status: 'Removed' })).toBe('Usuário removido (ex-membro)')
  })

  it('linhas por tipo ignoram quem tem valor zero', () => {
    const items = [
      makeParticipation({ userId: 'a', expenseAmount: 100, expensePercent: 100, incomeAmount: 0 }),
      makeParticipation({ userId: 'b', expenseAmount: 0, expensePercent: 0, incomeAmount: 50, incomePercent: 100 }),
    ]
    expect(participationRows(items, 'Expense').map((r) => r.userId)).toEqual(['a'])
    expect(participationRows(items, 'Income').map((r) => r.userId)).toEqual(['b'])
  })

  it('formata variação; base zero (null) vira "—"', () => {
    expect(formatChange(25)).toBe('+25,0%')
    expect(formatChange(-3)).toBe('−3,0%')
    expect(formatChange(0)).toBe('0,0%')
    expect(formatChange(null)).toBe('—')
  })

  it('detecta dashboard vazio', () => {
    expect(
      isGroupDashboardEmpty(
        makeGroupDashboard({
          totals: {
            expenses: 0,
            expensesPaid: 0,
            expensesPending: 0,
            income: 0,
            incomeReceived: 0,
            incomePending: 0,
            balance: 0,
          },
          createdBy: [],
          paidBy: [],
          monthly: [{ month: '2026-11', income: 0, expenses: 0 }],
        }),
      ),
    ).toBe(true)
    expect(isGroupDashboardEmpty(makeGroupDashboard())).toBe(false)
  })

  it('os textos distinguem as duas regras e negam dívida', () => {
    expect(PARTICIPATION_COPY.createdBy.title).toBe('Participação por quem cadastrou')
    expect(PARTICIPATION_COPY.paidBy.title).toBe('Participação por quem pagou')
    expect(PARTICIPATION_COPY.createdBy.rule).not.toBe(PARTICIPATION_COPY.paidBy.rule)
    expect(NOT_DEBT_NOTICE).toContain('NÃO representam dívida')
  })
})
