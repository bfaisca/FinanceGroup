import { describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import InputNumber from 'primevue/inputnumber'
import Select from 'primevue/select'
import { createPinia, setActivePinia } from 'pinia'
import { useAccountsStore } from '@/stores/accounts'
import { makeAccount, makeCreditCard } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import CreditCardForm from '../components/CreditCardForm.vue'

function setup(props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  setActivePinia(pinia)
  useAccountsStore().items = [makeAccount({ id: 'a1' }), makeAccount({ id: 'a2', name: 'Velha', isActive: false })]
  return mountWithPlugins(CreditCardForm, { props, pinia })
}

async function fill(wrapper: ReturnType<typeof setup>['wrapper'], closing: number | null, due: number | null) {
  await wrapper.find('#card-name').setValue('Roxinho')
  await wrapper.find('#card-limit').setValue('5000')
  const [closingInput, dueInput] = wrapper.findAllComponents(InputNumber)
  closingInput!.vm.$emit('update:modelValue', closing)
  dueInput!.vm.$emit('update:modelValue', due)
  await flushPromises()
}

describe('CreditCardForm', () => {
  it('valida campos obrigatórios e não emite', async () => {
    const { wrapper } = setup()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.find('#card-name-error').text()).toContain('Informe o nome')
    expect(wrapper.find('#card-limit-error').text()).toContain('limite')
    expect(wrapper.find('#card-closing-error').text()).toContain('fechamento')
    expect(wrapper.find('#card-due-error').text()).toContain('vencimento')
    expect(wrapper.find('#card-name').attributes('aria-invalid')).toBe('true')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it.each([
    [0, 12],
    [32, 12],
    [5, 0],
    [5, 40],
  ])('rejeita dias fora de 1–31 (fechamento %i, vencimento %i)', async (closing, due) => {
    const { wrapper } = setup()
    await fill(wrapper, closing, due)
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.text()).toContain('1 a 31')
    wrapper.unmount()
  })

  it('emite o request do contrato com limite numérico e conta opcional', async () => {
    const { wrapper } = setup()
    await fill(wrapper, 5, 12)
    wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'a1')
    await flushPromises()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      name: 'Roxinho',
      institution: null,
      creditLimit: 5000,
      closingDay: 5,
      dueDay: 12,
      accountId: 'a1',
    })
    wrapper.unmount()
  })

  it('oferece apenas contas ativas ao criar', async () => {
    const { wrapper } = setup()
    const options = wrapper.findComponent(Select).props('options') as { value: string }[]
    expect(options.map((o) => o.value)).toEqual(['a1'])
    wrapper.unmount()
  })

  it('na edição preenche os valores e mantém a conta associada inativa visível', async () => {
    const { wrapper } = setup({ card: makeCreditCard({ accountId: 'a2', creditLimit: 1234.5 }) })
    expect((wrapper.find('#card-name').element as HTMLInputElement).value).toBe('Nubank Roxinho')
    expect((wrapper.find('#card-limit').element as HTMLInputElement).value).toBe('1.234,50')
    const options = wrapper.findComponent(Select).props('options') as { value: string; label: string }[]
    expect(options.find((o) => o.value === 'a2')?.label).toContain('(inativa)')
    wrapper.unmount()
  })

  it('mapeia erros por campo do backend (400) e mostra erro geral', async () => {
    const { wrapper } = setup()
    await wrapper.setProps({
      fieldErrors: { name: ['Já existe um cartão com este nome.'], accountId: ['Conta inválida.'] },
    })
    expect(wrapper.find('#card-name-error').text()).toContain('Já existe')
    expect(wrapper.find('#card-account-error').text()).toContain('Conta inválida.')
    await wrapper.setProps({ errorMessage: 'Falha geral.' })
    expect(wrapper.find('[data-testid="form-error"]').text()).toContain('Falha geral.')
    wrapper.unmount()
  })
})
