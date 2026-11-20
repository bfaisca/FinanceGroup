import { describe, expect, it } from 'vitest'
import type SelectButton from 'primevue/selectbutton'
import { flushPromises } from '@vue/test-utils'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { createPinia, setActivePinia } from 'pinia'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useCreditCardsStore } from '@/stores/creditCards'
import { makeAccount, makeCategory, makeCreditCard, makeRecurrence } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import RecurrenceForm from '../components/RecurrenceForm.vue'

function setup(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  useCategoriesStore().items = [
    makeCategory({ id: 'c-lazer', name: 'Lazer' }),
    makeCategory({ id: 'c-sal', name: 'Salário', type: 'Income' }),
  ]
  useAccountsStore().items = [makeAccount()]
  const cards = useCreditCardsStore()
  cards.items = [makeCreditCard()]
  cards.loaded = true
  return mountWithPlugins(RecurrenceForm, { props: { today: '2026-11-18', ...props }, pinia }).wrapper
}

const selectById = (wrapper: ReturnType<typeof setup>, id: string) =>
  wrapper.findAllComponents(Select).find((s) => s.props('inputId') === id)!

describe('RecurrenceForm (criação)', () => {
  it('valida obrigatórios e o intervalo 1–365', async () => {
    const wrapper = setup()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#rec-description-error').text()).toContain('descrição')
    expect(wrapper.find('#rec-amount-error').text()).toContain('valor')
    expect(wrapper.find('#rec-category-error').text()).toContain('categoria')

    await wrapper.find('#rec-description').setValue('Netflix')
    await wrapper.find('#rec-amount').setValue('55,9')
    selectById(wrapper, 'rec-category').vm.$emit('update:modelValue', 'c-lazer')
    wrapper.findComponent(InputNumber).vm.$emit('update:modelValue', 400)
    await flushPromises()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#rec-interval-error').text()).toContain('1 a 365')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('"Personalizada" vira "a cada N dias" com rótulo explícito e prévia da frequência', async () => {
    const wrapper = setup()
    expect(wrapper.find('[data-testid="frequency-preview"]').text()).toContain('Mensal')
    expect(wrapper.find('label[for="rec-interval"]').text()).toContain('Intervalo')

    selectById(wrapper, 'rec-frequency').vm.$emit('update:modelValue', 'Custom')
    wrapper.findComponent(InputNumber).vm.$emit('update:modelValue', 10)
    await flushPromises()

    expect(wrapper.find('label[for="rec-interval"]').text()).toContain('A cada quantos dias')
    expect(wrapper.find('#rec-interval-hint').text()).toContain('dias')
    expect(wrapper.find('[data-testid="frequency-preview"]').text()).toContain('A cada 10 dias')
    wrapper.unmount()
  })

  it('emite o request completo da criação', async () => {
    const wrapper = setup()
    await wrapper.find('#rec-description').setValue('Netflix')
    await wrapper.find('#rec-amount').setValue('55,9')
    selectById(wrapper, 'rec-category').vm.$emit('update:modelValue', 'c-lazer')
    selectById(wrapper, 'rec-frequency').vm.$emit('update:modelValue', 'Custom')
    wrapper.findComponent(InputNumber).vm.$emit('update:modelValue', 10)
    await flushPromises()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({
      description: 'Netflix',
      type: 'Expense',
      amount: 55.9,
      categoryId: 'c-lazer',
      frequency: 'Custom',
      interval: 10,
      startDate: '2026-11-18',
      endDate: null,
    })
    wrapper.unmount()
  })

  it('cartão só aparece para despesa', async () => {
    const wrapper = setup()
    expect(wrapper.find('[data-testid="rec-paywith"]').exists()).toBe(true)
    wrapper
      .findComponent<typeof SelectButton>('fieldset.rec-form__type .p-selectbutton')
      .vm.$emit('update:modelValue', 'Income')
    await flushPromises()
    expect(wrapper.find('[data-testid="rec-paywith"]').exists()).toBe(false)
    expect(selectById(wrapper, 'rec-category').props('options')).toEqual([expect.objectContaining({ value: 'c-sal' })])
    wrapper.unmount()
  })

  it('mostra erros do backend por campo', async () => {
    const wrapper = setup()
    await wrapper.setProps({ fieldErrors: { startDate: ['Data muito antiga.'], interval: ['Intervalo inválido.'] } })
    expect(wrapper.find('#rec-start-error').text()).toContain('Data muito antiga.')
    expect(wrapper.find('#rec-interval-error').text()).toContain('Intervalo inválido.')
    wrapper.unmount()
  })
})

describe('RecurrenceForm (edição)', () => {
  it('tipo e início são imutáveis (somente leitura) e não vão no request; avisa sobre regeneração', async () => {
    const wrapper = setup({ recurrence: makeRecurrence({ categoryId: 'c-lazer' }) })

    const type = wrapper.find('#rec-type-readonly')
    expect((type.element as HTMLInputElement).disabled).toBe(true)
    expect((type.element as HTMLInputElement).value).toBe('Despesa')
    expect(wrapper.find('#rec-start').exists()).toBe(false)
    const start = wrapper.find('#rec-start-readonly')
    expect((start.element as HTMLInputElement).value).toBe('10/01/2026')
    expect((start.element as HTMLInputElement).disabled).toBe(true)
    expect(wrapper.text()).toContain('regeneradas conforme a nova regra')

    await wrapper.find('form').trigger('submit')
    const request = wrapper.emitted('submit')?.[0]?.[0] as Record<string, unknown>
    expect(request).toBeDefined()
    expect('type' in request).toBe(false)
    expect('startDate' in request).toBe(false)
    expect(request).toMatchObject({ frequency: 'Monthly', interval: 1, creditCardId: 'cc1' })
    wrapper.unmount()
  })

  it('o cartão da regra pode ser trocado (contrato permite)', () => {
    const wrapper = setup({ recurrence: makeRecurrence({ categoryId: 'c-lazer' }) })
    expect(wrapper.findComponent<typeof SelectButton>('[data-testid="rec-paywith"]').props('disabled')).toBe(false)
    wrapper.unmount()
  })
})
