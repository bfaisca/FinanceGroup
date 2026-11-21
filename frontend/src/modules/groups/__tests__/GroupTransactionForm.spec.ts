import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import Select from 'primevue/select'
import { createPinia, setActivePinia } from 'pinia'
import DateInput from '@/components/DateInput.vue'
import { useCategoriesStore } from '@/stores/categories'
import { makeCategory } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import GroupTransactionForm from '../components/GroupTransactionForm.vue'
import { makeGroupTransactionDetail } from './fixtures'

function setup(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  useCategoriesStore().items = [
    makeCategory({ id: 'c-alimentacao', name: 'Alimentação' }),
    makeCategory({ id: 'c-sal', name: 'Salário', type: 'Income' }),
  ]
  return mountWithPlugins(GroupTransactionForm, { props: { today: '2026-11-18', ...props }, pinia }).wrapper
}

describe('GroupTransactionForm', () => {
  it('cria movimentação enviando só campos do grupo (sem conta nem cartão)', async () => {
    const wrapper = setup()
    await wrapper.find('#gtx-description').setValue('Mercado')
    await wrapper.find('#gtx-amount').setValue('200,5')
    wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-alimentacao')
    await flushPromises()
    await wrapper.find('form').trigger('submit')

    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request).toEqual({
      description: 'Mercado',
      amount: 200.5,
      categoryId: 'c-alimentacao',
      paymentMethod: null,
      status: 'Pending',
      transactionDate: '2026-11-18',
      dueDate: null,
      notes: null,
    })
    expect(request).not.toHaveProperty('accountId')
    expect(request).not.toHaveProperty('creditCardId')
    expect(request).not.toHaveProperty('type')
    expect(request).not.toHaveProperty('payment')
    expect(request).not.toHaveProperty('recurrence')
    wrapper.unmount()
  })

  it('não tem seletor de tipo nem texto de receita (grupos só têm despesas)', () => {
    const wrapper = setup()
    expect(wrapper.text()).not.toMatch(/Receita/i)
    expect(wrapper.find('[aria-label="Tipo da movimentação"]').exists()).toBe(false)
    expect(wrapper.find('#gtx-type-readonly').exists()).toBe(false)
    wrapper.unmount()
  })

  async function fillBasics(wrapper: ReturnType<typeof setup>) {
    await wrapper.find('#gtx-description').setValue('IPTU')
    await wrapper.find('#gtx-amount').setValue('500')
    wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-alimentacao')
    await flushPromises()
  }

  it('vencimento aceita qualquer data: anterior à data da movimentação e passada', async () => {
    const wrapper = setup()
    await fillBasics(wrapper)
    const due = wrapper.findAllComponents(DateInput).find((c) => c.props('id') === 'gtx-due')!
    // Sem limite mínimo no seletor.
    expect(due.props('min')).toBeNull()
    due.vm.$emit('update:modelValue', '2025-01-01')
    await flushPromises()
    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request.dueDate).toBe('2025-01-01')
    expect(wrapper.text()).not.toContain('posterior à data da movimentação')
    wrapper.unmount()
  })

  it('"Já foi paga": mostra os campos, padrão = valor da conta, data = hoje, e envia `payment` com juros', async () => {
    const wrapper = setup()
    await fillBasics(wrapper)
    expect(wrapper.find('[data-testid="paid-fields"]').exists()).toBe(false)

    await wrapper.find('#gtx-paid').setValue(true)
    expect(wrapper.find('[data-testid="paid-fields"]').exists()).toBe(true)
    // Valor pago acompanha o valor da conta.
    expect((wrapper.find('#gtx-pay-amount').element as HTMLInputElement).value).toBe('500,00')
    // Hint de conta privada e de juros.
    expect(wrapper.text()).toContain('Sua conta é privada: o grupo não a vê')
    expect(wrapper.text()).toContain('só afeta o seu saldo individual')
    expect(wrapper.text()).toContain('Pagou atrasado com juros?')
    // A situação inicial some: o pagamento define a situação.
    expect(wrapper.find('#gtx-status').exists()).toBe(false)

    await wrapper.find('#gtx-pay-interest').setValue('23,50')
    const payDate = wrapper.findAllComponents(DateInput).find((c) => c.props('id') === 'gtx-pay-date')!
    expect(payDate.props('max')).toBe('2026-11-18')
    payDate.vm.$emit('update:modelValue', '2026-10-05')
    await flushPromises()
    await wrapper.find('form').trigger('submit')

    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request.payment).toEqual({
      amount: 500,
      interestAmount: 23.5,
      paymentDate: '2026-10-05',
      accountId: null,
      paymentMethod: null,
    })
    wrapper.unmount()
  })

  it('"Já foi paga": valor pago maior que a conta e data futura são rejeitados', async () => {
    const wrapper = setup()
    await fillBasics(wrapper)
    await wrapper.find('#gtx-paid').setValue(true)
    await wrapper.find('#gtx-pay-amount').setValue('600,00')
    const payDate = wrapper.findAllComponents(DateInput).find((c) => c.props('id') === 'gtx-pay-date')!
    payDate.vm.$emit('update:modelValue', '2026-11-19')
    await flushPromises()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.text()).toContain('maior que o valor da conta')
    expect(wrapper.text()).toContain('A data do pagamento não pode ser futura.')
    wrapper.unmount()
  })

  it('selects novos têm placeholder legível (método e conta do pagamento, frequência)', async () => {
    const wrapper = setup()
    await wrapper.find('#gtx-paid').setValue(true)
    await wrapper.find('#gtx-repeat').setValue(true)
    for (const select of wrapper.findAll('.p-select')) {
      expect(select.text().trim().length, select.html()).toBeGreaterThan(0)
    }
    expect(wrapper.text()).toContain('Não vincular a uma conta')
    expect(wrapper.text()).toContain('Mensal')
    wrapper.unmount()
  })

  it('"Repetir": frequência Mensal por padrão, prévia clara e envio de `recurrence`', async () => {
    const wrapper = setup()
    await wrapper.find('#gtx-description').setValue('IPTU')
    await wrapper.find('#gtx-amount').setValue('500')
    wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-alimentacao')
    const date = wrapper.findAllComponents(DateInput).find((c) => c.props('id') === 'gtx-date')!
    date.vm.$emit('update:modelValue', '2026-01-10')
    await wrapper.find('#gtx-repeat').setValue(true)
    await flushPromises()

    expect(wrapper.find('[data-testid="repeat-preview"]').text()).toMatch(
      /10 contas de R\$\s500,00, mensais, de 10\/01\/2026 a 10\/10\/2026 — total R\$\s5\.000,00/,
    )
    await wrapper.find('#gtx-occurrences').setValue('3')
    expect(wrapper.find('[data-testid="repeat-preview"]').text()).toMatch(
      /3 contas de R\$\s500,00, mensais, de 10\/01\/2026 a 10\/03\/2026/,
    )

    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request.recurrence).toEqual({ frequency: 'Monthly', occurrences: 3 })
    wrapper.unmount()
  })

  it('"Repetir" com quantidade fora de 2–60 não envia e mostra o erro', async () => {
    const wrapper = setup()
    await fillBasics(wrapper)
    await wrapper.find('#gtx-repeat').setValue(true)
    await wrapper.find('#gtx-occurrences').setValue('61')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.text()).toContain('de 2 a 60')
    wrapper.unmount()
  })

  it('repetir + já paga: avisa que o pagamento vale só para a 1ª ocorrência', async () => {
    const wrapper = setup()
    await fillBasics(wrapper)
    await wrapper.find('#gtx-paid').setValue(true)
    expect(wrapper.find('[data-testid="repeat-paid-warning"]').exists()).toBe(false)
    await wrapper.find('#gtx-repeat').setValue(true)
    expect(wrapper.find('[data-testid="repeat-paid-warning"]').text()).toContain('apenas para a 1ª ocorrência')
    wrapper.unmount()
  })

  it('erros do backend em `payment.*`/`recurrence.*` aparecem nos campos', async () => {
    const wrapper = setup()
    await wrapper.find('#gtx-paid').setValue(true)
    await wrapper.find('#gtx-repeat').setValue(true)
    await wrapper.setProps({
      fieldErrors: {
        'payment.interestAmount': ['Juros inválidos.'],
        'recurrence.occurrences': ['Quantidade inválida.'],
      },
    })
    await flushPromises()
    expect(wrapper.text()).toContain('Juros inválidos.')
    expect(wrapper.text()).toContain('Quantidade inválida.')
    wrapper.unmount()
  })

  it('não envia com erros e mostra mensagens por campo', async () => {
    const wrapper = setup()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    const text = wrapper.text()
    expect(text).toContain('Informe a descrição.')
    expect(text).toContain('Informe o valor.')
    expect(text).toContain('Escolha uma categoria.')
    wrapper.unmount()
  })

  it('todos os selects têm placeholder ou valor visível (nunca em branco)', async () => {
    const wrapper = setup()
    for (const select of wrapper.findAll('.p-select')) {
      expect(select.text().trim().length, select.html()).toBeGreaterThan(0)
    }
    expect(wrapper.text()).toContain('Escolha uma categoria')
    expect(wrapper.text()).toContain('Não informado')
    wrapper.unmount()
  })

  it('edição: sem seções de pagamento/repetição, avisa que edita só esta conta e respeita o valor já pago', async () => {
    const tx = makeGroupTransactionDetail({
      paidAmount: 100,
      remainingAmount: 100,
      series: { id: 's1', number: 3, total: 10 },
    })
    const wrapper = setup({ transaction: tx })

    expect(wrapper.find('[data-testid="edit-single-note"]').text()).toContain('Edita apenas esta conta')
    expect(wrapper.find('[data-testid="edit-single-note"]').text()).toContain('3/10')
    expect(wrapper.find('[data-testid="paid-section"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="repeat-section"]').exists()).toBe(false)
    await wrapper.find('#gtx-amount').setValue('50,00')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('não pode ser menor que o já pago')
    expect(wrapper.emitted('submit')).toBeUndefined()

    await wrapper.find('#gtx-amount').setValue('300,00')
    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request).not.toHaveProperty('type')
    expect(request).not.toHaveProperty('payment')
    expect(request).not.toHaveProperty('recurrence')
    expect(request.amount).toBe(300)
    wrapper.unmount()
  })

  it('erros do backend (400) aparecem no campo correspondente', async () => {
    const wrapper = setup()
    await wrapper.setProps({ fieldErrors: { description: ['Descrição inválida.'] } })
    await flushPromises()
    expect(wrapper.text()).toContain('Descrição inválida.')
    wrapper.unmount()
  })
})
