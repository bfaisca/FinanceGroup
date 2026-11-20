import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeAccount, makeCategory, makeCreditCard, makePlanDetail } from '@/test/fixtures'
import { mountWithPlugins, useDesktopViewport } from '@/test/mount'
import InstallmentPlanForm from '../components/InstallmentPlanForm.vue'
import InstallmentPlanFormDialog from '../components/InstallmentPlanFormDialog.vue'

const request = {
  description: 'Notebook',
  totalAmount: 3000,
  installmentCount: 12,
  firstDate: '2026-11-10',
  categoryId: 'c-compras',
  creditCardId: 'cc1',
  paymentMethod: 'CreditCard' as const,
  notes: null,
}

describe('InstallmentPlanFormDialog', () => {
  let mock: MockAdapter
  let restoreViewport: () => void
  beforeEach(() => {
    restoreViewport = useDesktopViewport()
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory({ id: 'c-compras', name: 'Compras' })])
    mock.onGet('/accounts').reply(200, [makeAccount()])
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
  })
  afterEach(() => {
    mock.restore()
    restoreViewport()
  })

  function open() {
    return mountWithPlugins(InstallmentPlanFormDialog, { props: { visible: true }, pinia: createPinia() }).wrapper
  }

  it('mostra as parcelas geradas pela API (sem cálculo local) após criar', async () => {
    const created = makePlanDetail({
      installments: [
        { ...makePlanDetail().installments[0]!, amount: 250.01, installment: { planId: 'p1', number: 1, count: 12 } },
        { ...makePlanDetail().installments[1]!, amount: 249.99, installment: { planId: 'p1', number: 2, count: 12 } },
      ],
    })
    mock.onPost('/installment-plans').reply(201, created)
    const wrapper = open()
    await flushPromises()

    wrapper.findComponent(InstallmentPlanForm).vm.$emit('submit', request)
    await flushPromises()

    expect(JSON.parse(mock.history.post[0]?.data as string)).toEqual(request)
    const result = document.body.querySelector('[data-testid="plan-result"]')!
    expect(result.textContent).toContain('Notebook')
    expect(result.textContent).toMatch(/R\$\s*3\.000,00 em 12 parcelas/)
    const rows = result.querySelectorAll('tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]!.textContent).toMatch(/R\$\s*250,01/)
    expect(rows[1]!.textContent).toMatch(/R\$\s*249,99/)
    expect(document.body.textContent).toContain('Parcelamento criado')
    expect(wrapper.emitted('saved')).toHaveLength(1)
    wrapper.unmount()
  })

  it('"Ver plano" fecha o diálogo e pede para abrir o plano criado', async () => {
    mock.onPost('/installment-plans').reply(201, makePlanDetail())
    const wrapper = open()
    await flushPromises()
    wrapper.findComponent(InstallmentPlanForm).vm.$emit('submit', request)
    await flushPromises()

    ;(
      Array.from(document.body.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Ver plano',
      ) as HTMLButtonElement
    ).click()
    await flushPromises()
    expect(wrapper.emitted('open-plan')).toEqual([['p1']])
    expect(wrapper.emitted('update:visible')?.at(-1)).toEqual([false])
    wrapper.unmount()
  })

  it('400 vira erro de campo; 422 aparece no topo do formulário', async () => {
    mock.onPost('/installment-plans').replyOnce(400, {
      title: 'Dados inválidos.',
      code: 'validation_error',
      status: 400,
      errors: { installmentCount: ['Quantidade inválida.'] },
    })
    const wrapper = open()
    await flushPromises()
    wrapper.findComponent(InstallmentPlanForm).vm.$emit('submit', request)
    await flushPromises()
    expect(document.body.querySelector('#plan-count-error')?.textContent).toContain('Quantidade inválida.')

    mock.onPost('/installment-plans').replyOnce(422, {
      title: 'Regra de negócio violada.',
      detail: 'O cartão está inativo.',
      code: 'business_rule_violation',
      status: 422,
    })
    wrapper.findComponent(InstallmentPlanForm).vm.$emit('submit', request)
    await flushPromises()
    expect(document.body.querySelector('[data-testid="form-error"]')?.textContent).toContain('O cartão está inativo.')
    expect(wrapper.emitted('saved')).toBeUndefined()
    wrapper.unmount()
  })
})
