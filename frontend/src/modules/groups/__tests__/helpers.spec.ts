import { describe, expect, it } from 'vitest'
import { groupTransactionActions } from '../actions'
import {
  displayName,
  formatChange,
  isGroupDashboardEmpty,
  monthlyOption,
  NOT_DEBT_NOTICE,
  PAID_BY_COPY,
  paidByRows,
  paidPendingSlices,
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
import { occurrenceDate, recurrencePreview, seriesBadge } from '../recurrence'
import {
  buildGroupCreateRequest,
  buildGroupPaymentRequest,
  buildGroupRequest,
  buildGroupUpdateRequest,
  mapGroupServerErrors,
  validateGroupForm,
  validateGroupPaymentForm,
  validateGroupTransactionForm,
  type GroupTransactionFormValues,
} from '../validation'
import { makeGroupDashboard, makeGroupMember, makeGroupTransaction, makePaidBy } from './fixtures'

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
    amount: '1.234,50',
    categoryId: 'c1',
    paymentMethod: null,
    status: 'Pending',
    transactionDate: '2026-11-05',
    dueDate: null,
    notes: '',
    paid: false,
    paymentDate: null,
    paymentAmount: '',
    paymentInterest: '',
    paymentAccountId: null,
    paymentMethodPaid: null,
    repeat: false,
    frequency: 'Monthly',
    occurrences: '10',
  }

  it('aceita valores válidos e monta a requisição SEM conta nem cartão', () => {
    expect(validateGroupTransactionForm(valid)).toEqual({})
    const request = buildGroupCreateRequest(valid)
    expect(request).toEqual({
      description: 'Mercado',
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

  it('valida descrição, valor, categoria, formato das datas e observações', () => {
    const errors = validateGroupTransactionForm({
      ...valid,
      description: '',
      amount: '0',
      categoryId: null,
      transactionDate: null,
      dueDate: '2026-13-45',
      notes: 'x'.repeat(1001),
    })
    expect(Object.keys(errors).sort()).toEqual([
      'amount',
      'categoryId',
      'description',
      'dueDate',
      'notes',
      'transactionDate',
    ])
  })

  it('vencimento é livre: passado e anterior à data da movimentação são aceitos (D-047)', () => {
    expect(validateGroupTransactionForm({ ...valid, dueDate: '2026-11-01' })).toEqual({})
    expect(
      validateGroupTransactionForm({ ...valid, dueDate: '2020-01-01' }, { creating: true, today: '2026-11-18' }),
    ).toEqual({})
  })

  it('"já paga": valida data (≤ hoje), valor ≤ valor da conta e juros ≥ 0', () => {
    const paid = { ...valid, paid: true, paymentDate: '2026-11-10' }
    const options = { creating: true, today: '2026-11-18' }
    expect(validateGroupTransactionForm(paid, options)).toEqual({})
    // Qualquer data passada vale.
    expect(validateGroupTransactionForm({ ...paid, paymentDate: '2025-01-01' }, options)).toEqual({})
    expect(validateGroupTransactionForm({ ...paid, paymentDate: '2026-11-19' }, options).paymentDate).toContain(
      'futura',
    )
    expect(validateGroupTransactionForm({ ...paid, paymentDate: null }, options).paymentDate).toBeDefined()
    expect(validateGroupTransactionForm({ ...paid, paymentAmount: '2.000,00' }, options).paymentAmount).toContain(
      'maior que o valor da conta',
    )
    expect(validateGroupTransactionForm({ ...paid, paymentAmount: '0' }, options).paymentAmount).toBeDefined()
    expect(validateGroupTransactionForm({ ...paid, paymentInterest: '-1' }, options).paymentInterest).toBeDefined()
    expect(validateGroupTransactionForm({ ...paid, paymentInterest: '23,50' }, options)).toEqual({})
  })

  it('envia `payment` só com "já paga": valor padrão = valor da conta, juros opcionais', () => {
    expect(buildGroupCreateRequest(valid)).not.toHaveProperty('payment')
    const paid = buildGroupCreateRequest({ ...valid, paid: true, paymentDate: '2026-11-10' })
    expect(paid.payment).toEqual({
      amount: 1234.5,
      interestAmount: 0,
      paymentDate: '2026-11-10',
      accountId: null,
      paymentMethod: null,
    })
    const withInterest = buildGroupCreateRequest({
      ...valid,
      paid: true,
      paymentDate: '2026-11-10',
      paymentAmount: '1.000,00',
      paymentInterest: '23,5',
      paymentAccountId: 'a1',
      paymentMethodPaid: 'Pix',
    })
    expect(withInterest.payment).toEqual({
      amount: 1000,
      interestAmount: 23.5,
      paymentDate: '2026-11-10',
      accountId: 'a1',
      paymentMethod: 'Pix',
    })
  })

  it('recorrência: 2–60 vezes; envia `recurrence` só com "repetir"; edição nunca envia payment/recurrence', () => {
    const options = { creating: true, today: '2026-11-18' }
    for (const bad of ['1', '61', 'abc', '', '2,5']) {
      expect(validateGroupTransactionForm({ ...valid, repeat: true, occurrences: bad }, options).occurrences).toContain(
        '2 a 60',
      )
    }
    for (const ok of ['2', '10', '60']) {
      expect(validateGroupTransactionForm({ ...valid, repeat: true, occurrences: ok }, options)).toEqual({})
    }
    expect(buildGroupCreateRequest(valid)).not.toHaveProperty('recurrence')
    expect(
      buildGroupCreateRequest({ ...valid, repeat: true, frequency: 'Quarterly', occurrences: ' 4 ' }).recurrence,
    ).toEqual({
      frequency: 'Quarterly',
      occurrences: 4,
    })
    const update = buildGroupUpdateRequest({ ...valid, paid: true, repeat: true, paymentDate: '2026-11-10' })
    expect(update).not.toHaveProperty('payment')
    expect(update).not.toHaveProperty('recurrence')
  })

  it('traduz chaves do backend (payment.*, recurrence.*) para os campos do formulário', () => {
    expect(
      mapGroupServerErrors({
        'payment.interestAmount': ['x'],
        'payment.paymentDate': ['y'],
        'recurrence.occurrences': ['z'],
        description: ['d'],
      }),
    ).toEqual({ paymentInterest: ['x'], paymentDate: ['y'], occurrences: ['z'], description: ['d'] })
  })

  it('impede reduzir o valor abaixo do já pago', () => {
    const errors = validateGroupTransactionForm({ ...valid, amount: '10,00' }, { minAmountCents: 5000 })
    expect(errors.amount).toContain('já pago')
  })
})

describe('validação do pagamento do grupo (D-048)', () => {
  const values = { amount: '300,00', interest: '', paymentDate: '2026-11-10', accountId: null, paymentMethod: null }
  const options = { remainingCents: 30000, today: '2026-11-18' }

  it('principal ≤ restante, data ≤ hoje, juros ≥ 0 (opcional)', () => {
    expect(validateGroupPaymentForm(values, options)).toEqual({})
    expect(validateGroupPaymentForm({ ...values, amount: '300,01' }, options).amount).toContain('maior que o restante')
    expect(validateGroupPaymentForm({ ...values, amount: '' }, options).amount).toBeDefined()
    expect(validateGroupPaymentForm({ ...values, paymentDate: '2026-11-19' }, options).paymentDate).toContain('futura')
    expect(validateGroupPaymentForm({ ...values, interest: '-5' }, options).interestAmount).toContain('negativos')
    expect(validateGroupPaymentForm({ ...values, interest: 'abc' }, options).interestAmount).toBeDefined()
    // Juros não entram na regra "≤ restante".
    expect(validateGroupPaymentForm({ ...values, interest: '9999,00' }, options)).toEqual({})
  })

  it('monta o pedido com principal e juros separados', () => {
    expect(buildGroupPaymentRequest({ ...values, amount: '500,00', interest: '23,50', paymentMethod: 'Pix' })).toEqual({
      amount: 500,
      interestAmount: 23.5,
      paymentDate: '2026-11-10',
      accountId: null,
      paymentMethod: 'Pix',
    })
    expect(buildGroupPaymentRequest(values).interestAmount).toBe(0)
  })
})

describe('recorrência (prévia visual)', () => {
  it('datas mensais com ajuste de fim de mês sem deriva', () => {
    expect([0, 1, 2, 3].map((i) => occurrenceDate('2026-01-31', 'Monthly', i))).toEqual([
      '2026-01-31',
      '2026-02-28',
      '2026-03-31',
      '2026-04-30',
    ])
    expect(occurrenceDate('2026-11-10', 'Annual', 2)).toBe('2028-11-10')
    expect(occurrenceDate('2026-11-10', 'Weekly', 3)).toBe('2026-12-01')
    expect(occurrenceDate('2026-11-10', 'Biweekly', 1)).toBe('2026-11-24')
    expect(occurrenceDate('2026-11-10', 'Bimonthly', 3)).toBe('2027-05-10')
    expect(occurrenceDate('2026-11-10', 'Quarterly', 1)).toBe('2027-02-10')
    expect(occurrenceDate('2026-11-10', 'Semiannual', 1)).toBe('2027-05-10')
    expect(occurrenceDate('lixo', 'Monthly', 1)).toBeNull()
  })

  it('resumo claro com quantidade, valor, frequência, período e total', () => {
    const preview = recurrencePreview('2026-01-10', 'Monthly', 10, 50000)
    expect(preview?.summary).toMatch(
      /^10 contas de R\$\s500,00, mensais, de 10\/01\/2026 a 10\/10\/2026 — total R\$\s5\.000,00$/,
    )
    expect(preview?.totalCents).toBe(500000)
  })

  it('sem prévia para dados incompletos ou fora do limite', () => {
    expect(recurrencePreview(null, 'Monthly', 10, 100)).toBeNull()
    expect(recurrencePreview('2026-01-10', 'Monthly', 1, 100)).toBeNull()
    expect(recurrencePreview('2026-01-10', 'Monthly', 61, 100)).toBeNull()
    expect(recurrencePreview('2026-01-10', 'Monthly', 5, null)).toBeNull()
  })

  it('selo da série', () => {
    expect(seriesBadge({ number: 3, total: 10 })).toBe('3/10')
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
    // Grupos só têm despesas (D-046): o filtro `type` da URL é ignorado.
    expect(filters).not.toHaveProperty('type')
    expect(toGroupListQuery(filters)).not.toHaveProperty('type')
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

  it('"Contas Pagas": linhas com principal, juros e total por pessoa', () => {
    const rows = paidByRows([
      makePaidBy({ userId: 'a', paidAmount: 523.5, interestAmount: 23.5, percent: 80 }),
      makePaidBy({ userId: 'b', name: 'Bia', status: 'Removed', paidAmount: 130, interestAmount: 0, percent: 20 }),
    ])
    expect(rows[0]).toMatchObject({ userId: 'a', paidAmount: 523.5, interestAmount: 23.5, principalAmount: 500 })
    expect(rows[1]).toMatchObject({ name: 'Bia (ex-membro)', former: true, principalAmount: 130 })
  })

  it('fatias Pago × Pendente usam expensesPaid e expensesPending', () => {
    const slices = paidPendingSlices(makeGroupDashboard().totals)
    expect(slices.map((s) => [s.key, s.value])).toEqual([
      ['paid', 423.5],
      ['pending', 600],
    ])
  })

  it('barras mensais: despesas e pago', () => {
    const option = monthlyOption([{ label: 'nov.', expenses: 1000, paid: 400 }])
    const series = option.series as { name: string; data: number[] }[]
    expect(series.map((s) => [s.name, s.data[0]])).toEqual([
      ['Despesas', 1000],
      ['Pago', 400],
    ])
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
          totals: { expenses: 0, expensesPaidPrincipal: 0, interestPaid: 0, expensesPaid: 0, expensesPending: 0 },
          paidBy: [],
          monthly: [{ month: '2026-11', expenses: 0, paid: 0 }],
        }),
      ),
    ).toBe(true)
    expect(isGroupDashboardEmpty(makeGroupDashboard())).toBe(false)
  })

  it('textos: "Contas Pagas" curto e aviso de não-dívida', () => {
    expect(PAID_BY_COPY.title).toBe('Contas Pagas')
    expect(PAID_BY_COPY).toEqual({ title: 'Contas Pagas' })
    expect(NOT_DEBT_NOTICE).toContain('não representam dívida')
  })
})
