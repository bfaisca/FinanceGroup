import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeDetail, makeRecurrence } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionDetailDrawer from '../components/TransactionDetailDrawer.vue'

async function open(detail = makeDetail()) {
  const mounted = mountWithPlugins(TransactionDetailDrawer, {
    props: { visible: true, transactionId: detail.id },
    pinia: createPinia(),
  })
  await flushPromises()
  return mounted.wrapper
}

function button(label: string): HTMLButtonElement | undefined {
  return Array.from(document.body.querySelectorAll('button')).find((b) => b.textContent?.trim() === label)
}

const toastAdd = vi.hoisted(() => vi.fn())
vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: toastAdd }) }))

describe('TransactionDetailDrawer', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('mostra progresso pago/restante e lista de pagamentos com tabela acessível', async () => {
    mock.onGet('/transactions/t1').reply(
      200,
      makeDetail({
        amount: 200,
        paidAmount: 50,
        remainingAmount: 150,
        payments: [
          {
            id: 'p1',
            amount: 50,
            paymentDate: '2026-11-10',
            accountId: null,
            accountName: 'Nubank',
            paymentMethod: 'Pix',
            createdAt: '2026-11-10T10:00:00Z',
          },
        ],
      }),
    )
    const wrapper = await open()
    const body = document.body
    expect(body.querySelector('[role="progressbar"]')?.getAttribute('aria-label')).toContain('25%')
    expect(body.textContent).toMatch(/Restante:\s*R\$\s*150,00/)
    expect(body.querySelectorAll('table th[scope="col"]').length).toBeGreaterThanOrEqual(3)
    expect(body.textContent).toContain('Nubank · Pix')
    wrapper.unmount()
  })

  it('cancelar pede confirmação e chama POST /cancel', async () => {
    mock.onGet('/transactions/t1').reply(200, makeDetail())
    mock.onPost('/transactions/t1/cancel').reply(200, makeDetail({ status: 'Canceled' }))
    const wrapper = await open()

    button('Cancelar movimentação')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('deixará de contar nos totais')
    expect(mock.history.post).toHaveLength(0)

    const confirm = Array.from(
      document.body.querySelectorAll('.confirm-dialog button[type="submit"]'),
    )[0] as HTMLButtonElement
    confirm.click()
    await flushPromises()

    expect(mock.history.post.map((r) => r.url)).toEqual(['/transactions/t1/cancel'])
    expect(wrapper.emitted('changed')).toHaveLength(1)
    expect(button('Reabrir')).toBeDefined()
    wrapper.unmount()
  })

  it('"Encerrar repetição" fecha a regra no dia anterior à movimentação, sem criar nada novo', async () => {
    mock
      .onGet('/transactions/t1')
      .reply(200, makeDetail({ type: 'Income', recurrenceRuleId: 'r1', transactionDate: '2026-12-05' }))
    mock.onGet('/recurrences/r1').reply(200, makeRecurrence({ id: 'r1', startDate: '2026-10-05', isActive: true }))
    mock.onPut('/recurrences/r1').reply(200, makeRecurrence({ id: 'r1' }))
    const wrapper = await open()

    button('Encerrar repetição')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('nenhum novo lançamento será gerado')
    expect(mock.history.put).toHaveLength(0)

    document.body.querySelector<HTMLButtonElement>('.confirm-dialog button[type="submit"]')!.click()
    await flushPromises()

    expect(JSON.parse(mock.history.put[0]!.data)).toMatchObject({ endDate: '2026-12-04' })
    expect(wrapper.emitted('changed')).toHaveLength(1)
    wrapper.unmount()
  })

  it('"Encerrar repetição" na primeira ocorrência desativa a regra', async () => {
    mock.onGet('/transactions/t1').reply(200, makeDetail({ recurrenceRuleId: 'r1', transactionDate: '2026-10-05' }))
    mock.onGet('/recurrences/r1').reply(200, makeRecurrence({ id: 'r1', startDate: '2026-10-05', isActive: true }))
    mock.onPost('/recurrences/r1/deactivate').reply(200, makeRecurrence({ id: 'r1', isActive: false }))
    const wrapper = await open()

    button('Encerrar repetição')!.click()
    await flushPromises()
    document.body.querySelector<HTMLButtonElement>('.confirm-dialog button[type="submit"]')!.click()
    await flushPromises()

    expect(mock.history.post.map((r) => r.url)).toEqual(['/recurrences/r1/deactivate'])
    wrapper.unmount()
  })

  it('não mostra "Encerrar repetição" em movimentação sem recorrência', async () => {
    mock.onGet('/transactions/t1').reply(200, makeDetail())
    const wrapper = await open()
    expect(button('Encerrar repetição')).toBeUndefined()
    wrapper.unmount()
  })

  it('trata 422 ao excluir: mostra o motivo do backend e recarrega', async () => {
    mock.onGet('/transactions/t1').reply(200, makeDetail())
    mock.onDelete('/transactions/t1').reply(422, {
      title: 'Regra de negócio violada.',
      detail: 'Não é possível excluir uma movimentação com pagamentos.',
      code: 'business_rule_violation',
      status: 422,
    })
    const wrapper = await open()

    button('Excluir')!.click()
    await flushPromises()
    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        severity: 'error',
        detail: 'Não é possível excluir uma movimentação com pagamentos.',
      }),
    )
    expect(wrapper.emitted('changed')).toBeUndefined()
    expect(mock.history.get.filter((r) => r.url === '/transactions/t1').length).toBe(2)
    wrapper.unmount()
  })

  it('carrega o histórico sob demanda, em linguagem legível', async () => {
    mock.onGet('/transactions/t1').reply(200, makeDetail())
    mock.onGet('/transactions/t1/history').reply(200, [
      {
        id: 'h2',
        action: 'Updated',
        field: 'amount',
        oldValue: '100.00',
        newValue: '150.00',
        createdAt: '2026-11-12T12:00:00Z',
      },
      { id: 'h1', action: 'Created', field: null, oldValue: null, newValue: null, createdAt: '2026-11-10T12:00:00Z' },
    ])
    const wrapper = await open()
    expect(mock.history.get.some((r) => r.url?.endsWith('/history'))).toBe(false)

    const tab = Array.from(document.body.querySelectorAll('[role="tab"]')).find((t) =>
      t.textContent?.includes('Histórico'),
    ) as HTMLElement
    tab.click()
    await flushPromises()

    const text = document.body.textContent ?? ''
    expect(text).toContain('Movimentação criada')
    expect(text).toMatch(/Valor: de R\$\s100,00 para R\$\s150,00/)
    wrapper.unmount()
  })

  it('mostra erro de carregamento com "Tentar novamente"', async () => {
    mock.onGet('/transactions/t1').replyOnce(404, { title: 'Recurso não encontrado.', code: 'not_found', status: 404 })
    const wrapper = await open()
    expect(document.body.querySelector('[role="alert"]')?.textContent).toContain('Recurso não encontrado.')
    expect(button('Tentar novamente')).toBeDefined()
    wrapper.unmount()
  })
})
