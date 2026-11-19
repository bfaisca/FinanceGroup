import { describe, expect, it } from 'vitest'
import { createPinia } from 'pinia'
import { makeDetail } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import PaymentForm from '../components/PaymentForm.vue'

function setup(detail = makeDetail({ amount: 200, paidAmount: 50, remainingAmount: 150, status: 'Pending' })) {
  return mountWithPlugins(PaymentForm, { props: { transaction: detail, today: '2026-11-18' }, pinia: createPinia() })
}

describe('PaymentForm', () => {
  it('valor padrão = restante e data padrão = hoje', () => {
    const { wrapper } = setup()
    expect((wrapper.find('#pay-amount').element as HTMLInputElement).value).toBe('150,00')
    expect((wrapper.find('#pay-date').element as HTMLInputElement).value).toBe('18/11/2026')
    wrapper.unmount()
  })

  it('emite o pagamento com o restante por padrão', async () => {
    const { wrapper } = setup()
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      amount: 150,
      paymentDate: '2026-11-18',
      accountId: null,
      paymentMethod: null,
    })
    wrapper.unmount()
  })

  it('permite pagamento parcial', async () => {
    const { wrapper } = setup()
    await wrapper.find('#pay-amount').setValue('20,25')
    await wrapper.find('form').trigger('submit')
    expect((wrapper.emitted('submit')?.[0]?.[0] as { amount: number }).amount).toBe(20.25)
    wrapper.unmount()
  })

  it('impede valor maior que o restante (UX) e não emite', async () => {
    const { wrapper } = setup()
    await wrapper.find('#pay-amount').setValue('150,01')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#pay-amount-error').text()).toMatch(/maior que o restante/)
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('rótulos mudam conforme o tipo (receita = recebimento)', () => {
    const { wrapper } = setup(makeDetail({ type: 'Income', amount: 10, remainingAmount: 10 }))
    expect(wrapper.text()).toContain('Registrar recebimento')
    expect(wrapper.text()).toContain('Valor recebido')
    wrapper.unmount()
  })

  it('mostra erro do backend no campo de valor', async () => {
    const { wrapper } = setup()
    await wrapper.setProps({ fieldErrors: { amount: ['O pagamento ultrapassa o restante.'] } })
    expect(wrapper.find('#pay-amount-error').text()).toContain('ultrapassa')
    wrapper.unmount()
  })
})
