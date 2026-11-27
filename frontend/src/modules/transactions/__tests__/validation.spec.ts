import { describe, expect, it } from 'vitest'
import {
  buildCreateRequest,
  buildPaymentRequest,
  buildUpdateRequest,
  defaultPaymentAmountText,
  validatePaymentForm,
  validateTransactionForm,
  type TransactionFormValues,
} from '../validation'

function values(partial: Partial<TransactionFormValues> = {}): TransactionFormValues {
  return {
    description: 'Mercado',
    type: 'Expense',
    amount: '1.234,56',
    categoryId: 'c1',
    accountId: null,
    paymentMethod: null,
    payWith: 'account',
    creditCardId: null,
    status: 'Pending',
    transactionDate: '2026-11-18',
    dueDate: null,
    notes: '',
    repeatMonthly: false,
    ...partial,
  }
}

describe('validateTransactionForm', () => {
  it('aceita um formulário válido', () => {
    expect(validateTransactionForm(values())).toEqual({})
  })

  it('exige descrição, valor, categoria e data', () => {
    const errors = validateTransactionForm(
      values({ description: '  ', amount: '', categoryId: null, transactionDate: null }),
    )
    expect(Object.keys(errors).sort()).toEqual(['amount', 'categoryId', 'description', 'transactionDate'])
  })

  it('valida limites de descrição e observações', () => {
    expect(validateTransactionForm(values({ description: 'x'.repeat(201) })).description).toMatch(/200/)
    expect(validateTransactionForm(values({ notes: 'x'.repeat(1001) })).notes).toMatch(/1000/)
  })

  it('rejeita valor zero, inválido ou com mais de 2 casas', () => {
    expect(validateTransactionForm(values({ amount: '0' })).amount).toMatch(/maior que zero/)
    expect(validateTransactionForm(values({ amount: 'abc' })).amount).toMatch(/válido/)
    expect(validateTransactionForm(values({ amount: '1,234' })).amount).toMatch(/válido/)
  })

  it('exige vencimento >= data da movimentação', () => {
    expect(validateTransactionForm(values({ dueDate: '2026-11-17' })).dueDate).toMatch(/posterior/)
    expect(validateTransactionForm(values({ dueDate: '2026-11-18' })).dueDate).toBeUndefined()
  })

  it('impede reduzir o valor abaixo do total já pago (D-023)', () => {
    const errors = validateTransactionForm(values({ amount: '40,00' }), { minAmountCents: 5000 })
    expect(errors.amount).toMatch(/menor que o já pago/)
    expect(validateTransactionForm(values({ amount: '50,00' }), { minAmountCents: 5000 }).amount).toBeUndefined()
  })
})

describe('requests', () => {
  it('criação converte o valor em number com 2 casas e usa Pending por padrão', () => {
    const request = buildCreateRequest(values({ description: '  Mercado ', notes: '  ', status: null }))
    expect(request).toEqual({
      description: 'Mercado',
      type: 'Expense',
      amount: 1234.56,
      categoryId: 'c1',
      accountId: null,
      paymentMethod: null,
      status: 'Pending',
      transactionDate: '2026-11-18',
      dueDate: null,
      notes: null,
    })
  })

  it('edição não envia `type` e só envia `status` quando escolhido', () => {
    const keep = buildUpdateRequest(values({ status: null }))
    expect('type' in keep).toBe(false)
    expect('status' in keep).toBe(false)
    expect(buildUpdateRequest(values({ status: 'Planned' })).status).toBe('Planned')
  })
})

describe('pagamento', () => {
  it('valor padrão é o restante', () => {
    expect(defaultPaymentAmountText(1234.5)).toBe('1.234,50')
    expect(defaultPaymentAmountText(0.3)).toBe('0,30')
  })

  it('limita ao restante e à data de hoje', () => {
    const base = { amount: '100,00', paymentDate: '2026-11-18', accountId: null, paymentMethod: null }
    const options = { remainingCents: 10000, today: '2026-11-18' }
    expect(validatePaymentForm(base, options)).toEqual({})
    expect(validatePaymentForm({ ...base, amount: '100,01' }, options).amount).toMatch(/maior que o restante/)
    expect(validatePaymentForm({ ...base, amount: '0' }, options).amount).toMatch(/maior que zero/)
    expect(validatePaymentForm({ ...base, paymentDate: '2026-11-19' }, options).paymentDate).toMatch(/futura/)
    expect(validatePaymentForm({ ...base, paymentDate: null }, options).paymentDate).toBeTruthy()
  })

  it('monta o request com number de 2 casas', () => {
    expect(
      buildPaymentRequest({ amount: '0,10', paymentDate: '2026-11-18', accountId: 'a1', paymentMethod: 'Pix' }),
    ).toEqual({
      amount: 0.1,
      paymentDate: '2026-11-18',
      accountId: 'a1',
      paymentMethod: 'Pix',
    })
  })
})

describe('formulário com cartão (Etapa 3)', () => {
  it('exige cartão e ignora o vencimento digitado', () => {
    const noCard = validateTransactionForm(values({ payWith: 'card' }))
    expect(noCard.creditCardId).toBeDefined()

    const ok = validateTransactionForm(values({ payWith: 'card', creditCardId: 'cc1', dueDate: '2000-01-01' }))
    expect(ok).toEqual({})
  })

  it('o request com cartão traz creditCardId e método CreditCard, sem accountId e dueDate', () => {
    const request = buildCreateRequest(
      values({ payWith: 'card', creditCardId: 'cc1', accountId: 'a1', dueDate: '2026-12-01', paymentMethod: 'Pix' }),
    )
    expect(request.creditCardId).toBe('cc1')
    expect(request.paymentMethod).toBe('CreditCard')
    expect('accountId' in request).toBe(false)
    expect('dueDate' in request).toBe(false)
  })

  it('receita nunca usa cartão, mesmo com payWith residual', () => {
    const request = buildCreateRequest(values({ type: 'Income', payWith: 'card', creditCardId: 'cc1' }))
    expect('creditCardId' in request).toBe(false)
    expect(validateTransactionForm(values({ type: 'Income', payWith: 'card' })).creditCardId).toBeUndefined()
  })

  it('a edição envia o mesmo cartão (imutável) e nunca o `type`', () => {
    const request = buildUpdateRequest(values({ payWith: 'card', creditCardId: 'cc1' }))
    expect(request.creditCardId).toBe('cc1')
    expect('type' in request).toBe(false)
  })
})
