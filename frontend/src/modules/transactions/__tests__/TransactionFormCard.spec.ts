import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import { createPinia, setActivePinia } from 'pinia'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useCreditCardsStore } from '@/stores/creditCards'
import { makeAccount, makeCategory, makeCreditCard, makeDetail } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionForm from '../components/TransactionForm.vue'

function setup(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  useCategoriesStore().items = [
    makeCategory({ id: 'c-moradia' }),
    makeCategory({ id: 'c-sal', name: 'Salário', type: 'Income' }),
  ]
  useAccountsStore().items = [makeAccount({ id: 'a1' })]
  const cards = useCreditCardsStore()
  cards.items = [makeCreditCard({ id: 'cc1' }), makeCreditCard({ id: 'cc2', name: 'Velho', isActive: false })]
  cards.loaded = true
  const mounted = mountWithPlugins(TransactionForm, { props: { today: '2026-11-18', ...props }, pinia })
  return mounted.wrapper
}

function payWith(wrapper: ReturnType<typeof setup>) {
  return wrapper.findComponent<typeof SelectButton>('[data-testid="tx-paywith"]')
}

async function chooseCard(wrapper: ReturnType<typeof setup>) {
  payWith(wrapper).vm.$emit('update:modelValue', 'card')
  await flushPromises()
}

async function fillBasics(wrapper: ReturnType<typeof setup>) {
  await wrapper.find('#tx-description').setValue('Mercado')
  await wrapper.find('#tx-amount').setValue('50,5')
  wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-moradia')
  await flushPromises()
}

describe('TransactionForm — "Pago com" (cartão de crédito)', () => {
  it('despesa oferece "Conta/dinheiro" e "Cartão de crédito"; receita não oferece cartão', async () => {
    const wrapper = setup()
    const options = wrapper
      .findAllComponents(SelectButton)
      .map((b) => (b.props('options') as { label: string }[]).map((o) => o.label))
    expect(options).toContainEqual(['Conta/dinheiro', 'Cartão de crédito'])

    const type = wrapper.findAllComponents(SelectButton)[0]!
    type.vm.$emit('update:modelValue', 'Income')
    await flushPromises()
    expect(wrapper.find('[data-testid="tx-paywith"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('ao escolher cartão oculta conta e vencimento, fixa o método e explica o vencimento', async () => {
    const wrapper = setup()
    expect(wrapper.find('#tx-account').exists()).toBe(true)
    expect(wrapper.find('#tx-due').exists()).toBe(true)

    await chooseCard(wrapper)

    expect(wrapper.find('#tx-account').exists()).toBe(false)
    expect(wrapper.find('#tx-due').exists()).toBe(false)
    expect(wrapper.find('#tx-method').exists()).toBe(false)
    const fixed = wrapper.find('#tx-method-fixed')
    expect((fixed.element as HTMLInputElement).value).toBe('Cartão de crédito')
    expect((fixed.element as HTMLInputElement).disabled).toBe(true)
    expect(wrapper.find('[data-testid="invoice-due-hint"]').text()).toContain(
      'O vencimento é calculado pela fatura do cartão.',
    )
    // Só cartões ativos na criação.
    const cardSelect = wrapper.findAllComponents(Select).find((s) => s.props('inputId') === 'tx-card')!
    expect((cardSelect.props('options') as { value: string }[]).map((o) => o.value)).toEqual(['cc1'])
    wrapper.unmount()
  })

  it('exige o cartão e envia creditCardId, método CreditCard e SEM accountId/dueDate', async () => {
    const wrapper = setup({ defaultAccountId: 'a1' })
    await fillBasics(wrapper)
    await chooseCard(wrapper)

    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#tx-card-error').text()).toContain('cartão')
    expect(wrapper.emitted('submit')).toBeUndefined()

    wrapper
      .findAllComponents(Select)
      .find((s) => s.props('inputId') === 'tx-card')!
      .vm.$emit('update:modelValue', 'cc1')
    await flushPromises()
    await wrapper.find('form').trigger('submit')

    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request).toMatchObject({ creditCardId: 'cc1', paymentMethod: 'CreditCard', type: 'Expense', amount: 50.5 })
    expect('accountId' in request).toBe(false)
    expect('dueDate' in request).toBe(false)
    wrapper.unmount()
  })

  it('voltar para conta/dinheiro devolve conta e vencimento e remove o cartão do request', async () => {
    const wrapper = setup()
    await fillBasics(wrapper)
    await chooseCard(wrapper)
    payWith(wrapper).vm.$emit('update:modelValue', 'account')
    await flushPromises()
    expect(wrapper.find('#tx-account').exists()).toBe(true)
    expect(wrapper.find('#tx-due').exists()).toBe(true)

    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect('creditCardId' in request).toBe(false)
    expect(request.paymentMethod).toBeNull()
    wrapper.unmount()
  })

  it('aviso quando não há cartões ativos', async () => {
    const wrapper = setup()
    const cards = useCreditCardsStore()
    cards.items = []
    await chooseCard(wrapper)
    expect(wrapper.find('[data-testid="no-cards-hint"]').text()).toContain('Cadastre um cartão')
    wrapper.unmount()
  })

  it('mostra o erro do backend em creditCardId', async () => {
    const wrapper = setup()
    await chooseCard(wrapper)
    await wrapper.setProps({ fieldErrors: { creditCardId: ['Cartão inválido.'] } })
    expect(wrapper.find('#tx-card-error').text()).toContain('Cartão inválido.')
    wrapper.unmount()
  })
})

describe('TransactionForm — edição de compra no cartão', () => {
  const cardTx = () =>
    makeDetail({
      creditCardId: 'cc1',
      creditCardName: 'Nubank Roxinho',
      paymentMethod: 'CreditCard',
      accountId: null,
      dueDate: '2026-12-12',
      categoryId: 'c-moradia',
    })

  it('o cartão é imutável (seletores desabilitados) e o vencimento não é editável', () => {
    const wrapper = setup({ transaction: cardTx() })
    expect(payWith(wrapper).props('disabled')).toBe(true)
    const cardSelect = wrapper.findAllComponents(Select).find((s) => s.props('inputId') === 'tx-card')!
    expect(cardSelect.props('disabled')).toBe(true)
    expect(cardSelect.props('modelValue')).toBe('cc1')
    expect(wrapper.find('#tx-due').exists()).toBe(false)
    expect(wrapper.text()).toContain('não pode ser alterada')
    wrapper.unmount()
  })

  it('reenvia o MESMO cartão, sem accountId/dueDate', async () => {
    const wrapper = setup({ transaction: cardTx() })
    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request.creditCardId).toBe('cc1')
    expect(request.paymentMethod).toBe('CreditCard')
    expect('accountId' in request).toBe(false)
    expect('dueDate' in request).toBe(false)
    wrapper.unmount()
  })

  it('despesa sem cartão não pode virar compra no cartão (seletor travado)', () => {
    const wrapper = setup({ transaction: makeDetail({ categoryId: 'c-moradia' }) })
    expect(payWith(wrapper).props('disabled')).toBe(true)
    expect(wrapper.find('#tx-account').exists()).toBe(true)
    wrapper.unmount()
  })

  it('parcela: valor e data desabilitados, com explicação, e o vencimento sem cartão continua editável', async () => {
    const wrapper = setup({
      transaction: makeDetail({ categoryId: 'c-moradia', installment: { planId: 'p1', number: 3, count: 12 } }),
    })
    expect((wrapper.find('#tx-amount').element as HTMLInputElement).disabled).toBe(true)
    expect(wrapper.find('#tx-date input, input#tx-date').exists()).toBe(true)
    expect((wrapper.find('input#tx-date').element as HTMLInputElement).disabled).toBe(true)
    expect(wrapper.find('[data-testid="installment-note"]').text()).toContain('parcela 3/12')
    expect(wrapper.find('#tx-due').exists()).toBe(true)
    wrapper.unmount()
  })
})
