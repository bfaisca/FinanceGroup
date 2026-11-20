import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeDetail, makePlanDetail } from '@/test/fixtures'
import { mountWithPlugins, useDesktopViewport } from '@/test/mount'
import InstallmentPlanDrawer from '../components/InstallmentPlanDrawer.vue'

const toastAdd = vi.hoisted(() => vi.fn())
vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: toastAdd }) }))

function button(label: string): HTMLButtonElement | undefined {
  return Array.from(document.body.querySelectorAll('button')).find((b) => b.textContent?.trim() === label)
}

async function open(plan = makePlanDetail()) {
  const { wrapper } = mountWithPlugins(InstallmentPlanDrawer, {
    props: { visible: true, planId: plan.id },
    pinia: createPinia(),
  })
  await flushPromises()
  return wrapper
}

describe('InstallmentPlanDrawer', () => {
  let mock: MockAdapter
  let restoreViewport: () => void
  beforeEach(() => {
    restoreViewport = useDesktopViewport()
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [])
    mock.onGet('/accounts').reply(200, [])
    mock.onGet('/credit-cards').reply(200, [])
    toastAdd.mockClear()
  })
  afterEach(() => {
    mock.restore()
    restoreViewport()
  })

  it('mostra resumo e a tabela de parcelas exatamente como a API devolve', async () => {
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    const wrapper = await open()

    const body = document.body
    expect(body.querySelector('h2')?.textContent).toContain('Notebook')
    const summary = body.querySelector('[data-testid="plan-summary"]')!.textContent!
    expect(summary).toMatch(/R\$\s*3\.000,00/)
    expect(summary).toMatch(/Restante\s*R\$\s*2\.500,00/)
    expect(summary).toContain('12 no total · 2 pagas · 10 em aberto')
    expect(summary).toContain('12/12/2026')
    expect(body.querySelector('[data-testid="plan-status"]')?.textContent).toContain('Em aberto')

    const rows = body.querySelectorAll('[data-testid="installments-table"] tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]!.textContent).toContain('1/12')
    expect(rows[0]!.textContent).toContain('Pago')
    expect(rows[1]!.textContent).toContain('2/12')
    expect(rows[1]!.textContent).toContain('12/12/2026')
    expect(rows[1]!.textContent).toContain('Pendente')
    wrapper.unmount()
  })

  it('erro de carregamento oferece "Tentar novamente"', async () => {
    mock
      .onGet('/installment-plans/p1')
      .replyOnce(404, { title: 'Recurso não encontrado.', code: 'not_found', status: 404 })
    const wrapper = await open()
    expect(document.body.querySelector('[role="alert"]')?.textContent).toContain('Recurso não encontrado.')

    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    button('Tentar novamente')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('Parcelas')
    wrapper.unmount()
  })

  it('"Abrir" a parcela abre o detalhe da movimentação (onde é possível pagar)', async () => {
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    mock
      .onGet('/transactions/i2')
      .reply(200, makeDetail({ id: 'i2', installment: { planId: 'p1', number: 2, count: 12 } }))
    const wrapper = await open()

    const openButtons = Array.from(document.body.querySelectorAll('[data-testid="installments-table"] tbody button'))
    expect(openButtons).toHaveLength(2)
    expect(openButtons[1]!.getAttribute('aria-label')).toBe('Abrir parcela 2 de 12')
    ;(openButtons[1] as HTMLButtonElement).click()
    await flushPromises()

    expect(mock.history.get.some((r) => r.url === '/transactions/i2')).toBe(true)
    expect(document.body.textContent).toContain('Registrar pagamento')
    wrapper.unmount()
  })

  it('cancelar o plano pede confirmação (parcelas pagas permanecem) e chama POST /cancel', async () => {
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    mock
      .onPost('/installment-plans/p1/cancel')
      .reply(200, makePlanDetail({ status: 'Canceled', openCount: 0, canceledCount: 10 }))
    const wrapper = await open()

    button('Cancelar plano')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('Parcelas que já têm pagamento registrado permanecem')
    expect(mock.history.post).toHaveLength(0)

    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/installment-plans/p1/cancel'])
    expect(wrapper.emitted('changed')).toHaveLength(1)
    expect(document.body.querySelector('[data-testid="plan-status"]')?.textContent).toContain('Cancelado')
    expect(button('Cancelar plano')).toBeUndefined()
    wrapper.unmount()
  })

  it('excluir o plano pede confirmação explicando a restrição e fecha ao concluir', async () => {
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    mock.onDelete('/installment-plans/p1').reply(204)
    const wrapper = await open()

    button('Excluir plano')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('só é possível se nenhuma parcela tiver pagamento')
    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()

    expect(mock.history.delete).toHaveLength(1)
    expect(wrapper.emitted('update:visible')?.at(-1)).toEqual([false])
    expect(wrapper.emitted('changed')).toHaveLength(1)
    wrapper.unmount()
  })

  it('422 ao excluir (há parcela paga) mostra o `detail` do backend e recarrega', async () => {
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    mock.onDelete('/installment-plans/p1').reply(422, {
      title: 'Regra de negócio violada.',
      detail: 'Há parcelas com pagamento registrado.',
      code: 'business_rule_violation',
      status: 422,
    })
    const wrapper = await open()

    button('Excluir plano')!.click()
    await flushPromises()
    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'error', detail: 'Há parcelas com pagamento registrado.' }),
    )
    expect(wrapper.emitted('changed')).toBeUndefined()
    expect(mock.history.get.filter((r) => r.url === '/installment-plans/p1')).toHaveLength(2)
    wrapper.unmount()
  })

  it('plano concluído não oferece "Cancelar plano"', async () => {
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail({ status: 'Completed', openCount: 0, paidCount: 12 }))
    const wrapper = await open()
    expect(button('Cancelar plano')).toBeUndefined()
    expect(button('Excluir plano')).toBeDefined()
    wrapper.unmount()
  })
})
