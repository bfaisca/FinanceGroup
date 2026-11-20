import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import { createPinia, setActivePinia } from 'pinia'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useCreditCardsStore } from '@/stores/creditCards'
import { makeAccount, makeCategory, makeCreditCard } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import InstallmentPlanForm from '../components/InstallmentPlanForm.vue'

function setup(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  useCategoriesStore().items = [
    makeCategory({ id: 'c-compras', name: 'Compras' }),
    makeCategory({ id: 'c-sal', name: 'Salário', type: 'Income' }),
  ]
  useAccountsStore().items = [makeAccount()]
  const cards = useCreditCardsStore()
  cards.items = [makeCreditCard()]
  cards.loaded = true
  return mountWithPlugins(InstallmentPlanForm, { props: { today: '2026-11-18', ...props }, pinia }).wrapper
}

async function fillValid(wrapper: ReturnType<typeof setup>, count: number | null = 12) {
  await wrapper.find('#plan-description').setValue('Notebook')
  await wrapper.find('#plan-total').setValue('3000')
  wrapper.findComponent(InputNumber).vm.$emit('update:modelValue', count)
  wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-compras')
  await flushPromises()
}

describe('InstallmentPlanForm', () => {
  it('valida obrigatórios e não emite', async () => {
    const wrapper = setup()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#plan-description-error').text()).toContain('descrição')
    expect(wrapper.find('#plan-total-error').text()).toContain('valor total')
    expect(wrapper.find('#plan-count-error').text()).toContain('número de parcelas')
    expect(wrapper.find('#plan-category-error').text()).toContain('categoria')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it.each([1, 121])('rejeita %i parcelas', async (count) => {
    const wrapper = setup()
    await fillValid(wrapper, count)
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#plan-count-error').text()).toContain('2 a 120')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('emite o request com 1ª data padrão = hoje, só categorias de despesa', async () => {
    const wrapper = setup()
    const category = wrapper.findAllComponents(Select)[0]!
    expect((category.props('options') as { value: string }[]).map((o) => o.value)).toEqual(['c-compras'])

    await fillValid(wrapper, 12)
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      description: 'Notebook',
      totalAmount: 3000,
      installmentCount: 12,
      firstDate: '2026-11-18',
      categoryId: 'c-compras',
      notes: null,
      accountId: null,
      paymentMethod: null,
    })
    wrapper.unmount()
  })

  it('com cartão: oculta conta, explica o vencimento e envia só o cartão', async () => {
    const wrapper = setup()
    await fillValid(wrapper, 6)
    wrapper.findComponent<typeof SelectButton>('[data-testid="plan-paywith"]').vm.$emit('update:modelValue', 'card')
    await flushPromises()
    expect(wrapper.find('#plan-account').exists()).toBe(false)
    expect(wrapper.text()).toContain('O vencimento é calculado pela fatura do cartão.')

    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#plan-card-error').exists()).toBe(true)

    wrapper
      .findAllComponents(Select)
      .find((s) => s.props('inputId') === 'plan-card')!
      .vm.$emit('update:modelValue', 'cc1')
    await flushPromises()
    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as unknown as Record<string, unknown>
    expect(request).toMatchObject({ creditCardId: 'cc1', paymentMethod: 'CreditCard', installmentCount: 6 })
    expect('accountId' in request).toBe(false)
    wrapper.unmount()
  })

  it('mostra erros por campo do backend', async () => {
    const wrapper = setup()
    await wrapper.setProps({
      fieldErrors: { installmentCount: ['Quantidade inválida.'], firstDate: ['Data inválida.'] },
    })
    expect(wrapper.find('#plan-count-error').text()).toContain('Quantidade inválida.')
    expect(wrapper.find('#plan-first-date-error').text()).toContain('Data inválida.')
    expect(wrapper.findAllComponents(SelectButton).length).toBeGreaterThan(0)
    wrapper.unmount()
  })
})
