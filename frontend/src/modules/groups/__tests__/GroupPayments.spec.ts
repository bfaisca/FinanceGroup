import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import Select from 'primevue/select'
import ToastEventBus from 'primevue/toasteventbus'
import { http } from '@/services/http'
import { useCategoriesStore } from '@/stores/categories'
import { useClockStore } from '@/stores/clock'
import { makeAccount, makeCategory } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import GroupTransactionFormDialog from '../components/GroupTransactionFormDialog.vue'
import GroupTransactionsTab from '../components/GroupTransactionsTab.vue'
import { makeGroupDetail, makeGroupPayment, makeGroupTransaction, makeGroupTransactionDetail } from './fixtures'
import { button, mountAt } from './helpers'

function pageOf(items = [makeGroupTransaction()]) {
  return { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 }
}

const mountTab = (url = '/groups/g1?tab=transactions') =>
  mountAt(
    GroupTransactionsTab,
    url,
    { path: '/groups/:id', name: 'group-detail' },
    { props: { group: makeGroupDetail() } },
  )

const set = (selector: string, value: string) => {
  const el = document.body.querySelector<HTMLInputElement>(selector)!
  el.value = value
  el.dispatchEvent(new Event('input'))
}

/** Conta de R$ 500 paga com R$ 23,50 de juros (total R$ 523,50), 3ª de 10. */
const paidWithInterest = (partial = {}) =>
  makeGroupTransactionDetail({
    id: 'gt1',
    description: 'IPTU',
    amount: 500,
    paidAmount: 500,
    interestPaid: 23.5,
    totalPaid: 523.5,
    remainingAmount: 0,
    status: 'Paid',
    series: { id: 's1', number: 3, total: 10 },
    permissions: { canEdit: true, canCancel: true, canDelete: false, canPay: false },
    payments: [
      makeGroupPayment({
        id: 'p-own',
        amount: 500,
        interestAmount: 23.5,
        totalAmount: 523.5,
        paidBy: { userId: 'u1', name: 'Maria Silva' },
        canEdit: true,
        canRemove: true,
      }),
      ...[],
    ],
    ...partial,
  })

describe('lista, detalhe e pagamentos do grupo (v1.5)', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory({ id: 'c-alimentacao', name: 'Alimentação' })])
    mock.onGet('/accounts').reply(200, [makeAccount({ id: 'a1', name: 'Nubank' })])
    mock.onGet('/groups/g1/members').reply(200, [])
  })
  afterEach(() => mock.restore())

  it('lista mostra "Original · Pago (+ juros)" e o selo da série "3/10"', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf([paidWithInterest()]))
    const { wrapper } = await mountTab()

    const text = wrapper.text()
    expect(text).toMatch(/Original\s*R\$\s500,00/)
    expect(text).toMatch(/Pago\s*R\$\s523,50/)
    expect(text).toMatch(/\(\+ juros R\$\s23,50\)/)
    expect(wrapper.find('[data-testid="series-badge"]').text()).toContain('3/10')
    expect(wrapper.find('[data-testid="series-badge"]').text()).toContain('Conta 3 de 10')
    wrapper.unmount()
  })

  it('lista sem pagamento não mostra "Pago" nem selo', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    const { wrapper } = await mountTab()
    expect(wrapper.find('[data-testid="paid-summary"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="series-badge"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('filtros não têm "Tipo" nem categorias de receita', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    const { wrapper } = await mountTab()
    expect(wrapper.find('#gfilter-type').exists()).toBe(false)
    expect(wrapper.text()).not.toContain('Todos os tipos')
    wrapper.unmount()
  })

  async function openDetail(detail = paidWithInterest()) {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf([detail]))
    mock.onGet(`/groups/g1/transactions/${detail.id}`).reply(200, detail)
    return mountTab(`/groups/g1?tab=transactions&open=${detail.id}`)
  }

  it('detalhe mostra original × pago, juros, série e cada pagamento com principal, juros, total e quem', async () => {
    const { wrapper } = await openDetail()
    const body = document.body
    const text = body.textContent ?? ''

    expect(text).toMatch(/Original\s*R\$\s500,00\s*·\s*Pago\s*R\$\s523,50\s*\(\+ juros R\$\s23,50\)/)
    expect(body.querySelector('[data-testid="detail-series"]')?.textContent).toContain('Conta 3 de 10')
    expect(body.textContent).not.toMatch(/Receita|recebeu|Recebimento/)
    const row = body.querySelector('.gtx-drawer__payments tbody tr')!.textContent ?? ''
    expect(row).toContain('Maria Silva')
    expect(row).toMatch(/R\$\s500,00/)
    expect(row).toMatch(/R\$\s23,50/)
    expect(row).toMatch(/R\$\s523,50/)
    wrapper.unmount()
  })

  it('botões editar/remover do pagamento só aparecem com canEdit/canRemove', async () => {
    const detail = paidWithInterest({
      payments: [
        makeGroupPayment({
          id: 'p-own',
          canEdit: true,
          canRemove: true,
          paidBy: { userId: 'u1', name: 'Maria Silva' },
        }),
        makeGroupPayment({ id: 'p-edit-only', canEdit: true, canRemove: false }),
        makeGroupPayment({ id: 'p-none', canEdit: false, canRemove: false }),
      ],
    })
    const { wrapper } = await openDetail(detail)
    const rows = Array.from(document.body.querySelectorAll('.gtx-drawer__payments tbody tr'))
    const labels = (row: Element) => Array.from(row.querySelectorAll('button')).map((b) => b.getAttribute('aria-label'))
    expect(labels(rows[0]!)).toHaveLength(2)
    expect(labels(rows[1]!)).toHaveLength(1)
    expect(labels(rows[1]!)[0]).toMatch(/^Editar/)
    expect(labels(rows[2]!)).toHaveLength(0)
    wrapper.unmount()
  })

  it('registra pagamento com principal e juros separados (principal ≤ restante; juros fora da regra)', async () => {
    const detail = makeGroupTransactionDetail({ amount: 500, remainingAmount: 500 })
    mock.onPost('/groups/g1/transactions/gt1/payments').reply(201, detail)
    const { wrapper } = await openDetail(detail)

    await button(document.body, 'Registrar pagamento')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain(
      'Pagou atrasado com juros? Informe o valor da conta e os juros separados.',
    )

    const form = () => document.body.querySelector<HTMLFormElement>('form[aria-label="Registrar pagamento"]')!
    set('#gpay-amount', '600,00')
    form().dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(document.body.textContent).toContain('O valor da conta não pode ser maior que o restante')
    expect(mock.history.post).toHaveLength(0)

    set('#gpay-amount', '500,00')
    set('#gpay-interest', '23,50')
    form().dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(JSON.parse(mock.history.post[0]?.data)).toMatchObject({
      amount: 500,
      interestAmount: 23.5,
      paymentDate: '2026-11-18',
    })
    wrapper.unmount()
  })

  it('editar o próprio pagamento faz PUT com valores atuais pré-preenchidos e permite corrigir juros', async () => {
    const detail = paidWithInterest()
    mock.onPut('/groups/g1/transactions/gt1/payments/p-own').reply(200, detail)
    const { wrapper } = await openDetail(detail)

    document.body.querySelector<HTMLButtonElement>('button[aria-label^="Editar seu pagamento"]')!.click()
    await flushPromises()

    expect(document.body.textContent).toContain('Editar pagamento')
    expect(document.body.querySelector<HTMLInputElement>('#gpay-amount')!.value).toBe('500,00')
    expect(document.body.querySelector<HTMLInputElement>('#gpay-interest')!.value).toBe('23,50')
    expect(document.body.querySelector<HTMLInputElement>('#gpay-date input, input#gpay-date')!.value).toBe('10/11/2026')

    set('#gpay-interest', '30,00')
    document.body
      .querySelector<HTMLFormElement>('form[aria-label="Editar pagamento"]')!
      .dispatchEvent(new Event('submit'))
    await flushPromises()

    expect(mock.history.put).toHaveLength(1)
    expect(JSON.parse(mock.history.put[0]?.data)).toMatchObject({
      amount: 500,
      interestAmount: 30,
      paymentDate: '2026-11-10',
    })
    wrapper.unmount()
  })

  it('ao editar, o principal do próprio pagamento volta a contar como disponível (restante 0 aceita 500)', async () => {
    const { wrapper } = await openDetail()
    document.body.querySelector<HTMLButtonElement>('button[aria-label^="Editar seu pagamento"]')!.click()
    await flushPromises()
    set('#gpay-amount', '501,00')
    document.body
      .querySelector<HTMLFormElement>('form[aria-label="Editar pagamento"]')!
      .dispatchEvent(new Event('submit'))
    await flushPromises()
    expect(document.body.textContent).toContain('não pode ser maior que o restante (R$')
    expect(mock.history.put).toHaveLength(0)
    wrapper.unmount()
  })

  it('histórico mostra PaymentUpdated', async () => {
    mock.onGet('/groups/g1/transactions/gt1/history').reply(200, [
      {
        id: 'h1',
        action: 'PaymentUpdated',
        field: 'payment',
        oldValue: '500.00 (2026-11-10)',
        newValue: '500.00 + 30.00 (2026-11-10)',
        actorName: 'Maria Silva',
        createdAt: '2026-11-11T10:00:00Z',
      },
    ])
    const { wrapper } = await openDetail()
    Array.from(document.body.querySelectorAll<HTMLElement>('[role="tab"]'))
      .find((t) => t.textContent?.trim() === 'Histórico')!
      .click()
    await flushPromises()

    const history = document.body.querySelector('ol.history')!.textContent ?? ''
    expect(history).toContain('Pagamento alterado')
    expect(history).toMatch(/R\$\s500,00 em 10\/11\/2026/)
    expect(history).toMatch(/R\$\s30,00 de juros\/multa/)
    expect(history).toContain('por Maria Silva')
    wrapper.unmount()
  })

  describe('nova movimentação já paga e recorrente (diálogo)', () => {
    it('envia `payment` e `recurrence` juntos, avisa sobre a 1ª ocorrência e informa as contas criadas', async () => {
      const created = makeGroupTransactionDetail({ series: { id: 's1', number: 1, total: 3 } })
      mock.onPost('/groups/g1/transactions').reply(201, created)
      const toasts: { summary: string }[] = []
      const collect = (m: { summary: string }) => toasts.push(m)
      ToastEventBus.on('add', collect)

      const pinia = createPinia()
      useClockStore(pinia).today = '2026-11-18'
      useCategoriesStore(pinia).items = [makeCategory({ id: 'c-alimentacao', name: 'Alimentação' })]
      const { wrapper } = mountWithPlugins(GroupTransactionFormDialog, {
        props: { groupId: 'g1', visible: true },
        pinia,
      })
      await flushPromises()

      set('#gtx-description', 'IPTU')
      set('#gtx-amount', '500,00')
      wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-alimentacao')
      for (const selector of ['#gtx-paid', '#gtx-repeat']) {
        const el = document.body.querySelector<HTMLInputElement>(selector)!
        el.checked = true
        el.dispatchEvent(new Event('change'))
      }
      await flushPromises()
      set('#gtx-occurrences', '3')
      set('#gtx-pay-interest', '23,50')
      await flushPromises()

      expect(document.body.querySelector('[data-testid="repeat-paid-warning"]')?.textContent).toContain('1ª ocorrência')
      document.body.querySelector('form')!.dispatchEvent(new Event('submit'))
      await flushPromises()

      const body = JSON.parse(mock.history.post[0]?.data)
      expect(body.payment).toMatchObject({ amount: 500, interestAmount: 23.5, paymentDate: '2026-11-18' })
      expect(body.recurrence).toEqual({ frequency: 'Monthly', occurrences: 3 })
      expect(body).not.toHaveProperty('type')
      expect(toasts.map((t) => t.summary)).toContain('3 contas criadas')
      ToastEventBus.off('add', collect)
      wrapper.unmount()
    })

    it('422 do backend aparece no formulário e mantém o diálogo aberto', async () => {
      mock.onPost('/groups/g1/transactions').reply(422, { detail: 'Grupo arquivado.', code: 'business_rule_violation' })
      const pinia = createPinia()
      useCategoriesStore(pinia).items = [makeCategory({ id: 'c-alimentacao', name: 'Alimentação' })]
      const { wrapper } = mountWithPlugins(GroupTransactionFormDialog, {
        props: { groupId: 'g1', visible: true },
        pinia,
      })
      await flushPromises()
      set('#gtx-description', 'IPTU')
      set('#gtx-amount', '500,00')
      wrapper.findAllComponents(Select)[0]!.vm.$emit('update:modelValue', 'c-alimentacao')
      await flushPromises()
      document.body.querySelector('form')!.dispatchEvent(new Event('submit'))
      await flushPromises()
      expect(document.body.querySelector('[data-testid="form-error"]')?.textContent).toContain('Grupo arquivado.')
      wrapper.unmount()
    })
  })
})
