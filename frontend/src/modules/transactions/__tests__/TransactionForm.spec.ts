import { beforeEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import Select from 'primevue/select'
import { createPinia, setActivePinia } from 'pinia'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { makeAccount, makeCategory, makeDetail } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionForm from '../components/TransactionForm.vue'

function setup(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  const categories = useCategoriesStore()
  categories.items = [
    makeCategory({ id: 'c-moradia', name: 'Moradia' }),
    makeCategory({ id: 'c-sub', name: 'Aluguel', parentCategoryId: 'c-moradia', isDefault: false }),
    makeCategory({ id: 'c-salario', name: 'Salário', type: 'Income' }),
    makeCategory({ id: 'c-off', name: 'Antiga', isActive: false }),
  ]
  useAccountsStore().items = [makeAccount({ id: 'a1' })]
  return mountWithPlugins(TransactionForm, { props: { today: '2026-11-18', ...props }, pinia })
}

async function fillValid(wrapper: ReturnType<typeof setup>['wrapper']) {
  await wrapper.find('#tx-description').setValue('Mercado')
  await wrapper.find('#tx-amount').setValue('50,5')
  const category = wrapper.findAllComponents(Select)[0]!
  category.vm.$emit('update:modelValue', 'c-moradia')
  await flushPromises()
}

describe('TransactionForm (criação)', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('valida campos obrigatórios e não emite', async () => {
    const { wrapper } = setup()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('#tx-description-error').text()).toContain('Informe a descrição.')
    expect(wrapper.find('#tx-amount-error').text()).toContain('Informe o valor.')
    expect(wrapper.find('#tx-category-error').text()).toContain('Escolha uma categoria.')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('liga os erros aos campos via aria-describedby / aria-invalid', async () => {
    const { wrapper } = setup()
    await wrapper.find('form').trigger('submit')
    const input = wrapper.find('#tx-description')
    expect(input.attributes('aria-describedby')).toContain('tx-description-error')
    expect(input.attributes('aria-invalid')).toBe('true')
    wrapper.unmount()
  })

  it('emite o request com valor numérico, data padrão = hoje e situação Pending', async () => {
    const { wrapper } = setup()
    await fillValid(wrapper)
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      description: 'Mercado',
      type: 'Expense',
      amount: 50.5,
      categoryId: 'c-moradia',
      accountId: null,
      paymentMethod: null,
      status: 'Pending',
      transactionDate: '2026-11-18',
      dueDate: null,
      notes: null,
    })
    wrapper.unmount()
  })

  it('só oferece categorias ativas do tipo escolhido (com subcategorias)', async () => {
    const { wrapper } = setup()
    const category = wrapper.findAllComponents(Select)[0]!
    const options = category.props('options') as { value: string; label: string }[]
    expect(options.map((o) => o.value)).toEqual(['c-moradia', 'c-sub'])
    expect(options[1]?.label).toBe('Moradia › Aluguel')
    wrapper.unmount()
  })

  it('trocar o tipo limpa a categoria e passa a listar categorias de receita', async () => {
    const { wrapper } = setup()
    await fillValid(wrapper)
    const radios = wrapper.findAll('[role="radio"], .p-togglebutton, button.p-togglebutton')
    const incomeButton = radios.find((b) => b.text() === 'Receita')
    expect(incomeButton).toBeTruthy()
    await incomeButton!.trigger('click')
    await flushPromises()

    const category = wrapper.findAllComponents(Select)[0]!
    expect((category.props('options') as { value: string }[]).map((o) => o.value)).toEqual(['c-salario'])
    expect(category.props('modelValue')).toBeNull()
    wrapper.unmount()
  })

  it('exibe erros do backend (400) nos campos', async () => {
    const { wrapper } = setup()
    await wrapper.setProps({ fieldErrors: { amount: ['Valor muito alto.'], dueDate: ['Vencimento inválido.'] } })
    expect(wrapper.find('#tx-amount-error').text()).toContain('Valor muito alto.')
    expect(wrapper.find('#tx-due-error').text()).toContain('Vencimento inválido.')
    wrapper.unmount()
  })

  it('exibe a mensagem (detail de 422) no topo', async () => {
    const { wrapper } = setup()
    await wrapper.setProps({ errorMessage: 'A categoria está inativa.' })
    expect(wrapper.find('[data-testid="form-error"]').text()).toContain('A categoria está inativa.')
    wrapper.unmount()
  })
})

describe('TransactionForm (edição)', () => {
  it('o tipo é imutável: campo desativado e ausente do request', async () => {
    const { wrapper } = setup({ transaction: makeDetail({ categoryId: 'c-moradia', amount: 1500 }) })

    const type = wrapper.find('input[name="type"]')
    expect(type.exists()).toBe(true)
    expect((type.element as HTMLInputElement).disabled).toBe(true)
    expect((type.element as HTMLInputElement).value).toBe('Despesa')
    expect(wrapper.text()).toContain('não pode ser alterado')

    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request).toBeDefined()
    expect('type' in request).toBe(false)
    expect(request.amount).toBe(1500)
    expect('status' in request).toBe(false)
    wrapper.unmount()
  })

  it('não permite reduzir o valor abaixo do já pago', async () => {
    const { wrapper } = setup({
      transaction: makeDetail({ amount: 100, paidAmount: 60, remainingAmount: 40, status: 'Pending' }),
    })
    await wrapper.find('#tx-amount').setValue('50,00')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('#tx-amount-error').text()).toMatch(/menor que o já pago/)
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('oculta a situação inicial quando já há pagamentos', () => {
    const withPayments = setup({ transaction: makeDetail({ paidAmount: 10, remainingAmount: 1490 }) })
    expect(withPayments.wrapper.find('#tx-status').exists()).toBe(false)
    withPayments.wrapper.unmount()

    const without = setup({ transaction: makeDetail() })
    expect(without.wrapper.text()).toContain('Situação')
    without.wrapper.unmount()
  })
})
