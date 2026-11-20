import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import Select from 'primevue/select'
import { createPinia, setActivePinia } from 'pinia'
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
      type: 'Expense',
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

  it('edição: tipo imutável, não envia `type` e respeita o valor já pago', async () => {
    const tx = makeGroupTransactionDetail({ paidAmount: 100, remainingAmount: 100 })
    const wrapper = setup({ transaction: tx })

    expect(wrapper.find('#gtx-type-readonly').attributes('disabled')).toBeDefined()
    await wrapper.find('#gtx-amount').setValue('50,00')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.text()).toContain('não pode ser menor que o já pago')
    expect(wrapper.emitted('submit')).toBeUndefined()

    await wrapper.find('#gtx-amount').setValue('300,00')
    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request).not.toHaveProperty('type')
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
