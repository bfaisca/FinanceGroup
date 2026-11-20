import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { http } from '@/services/http'
import { makeAccount, makeCategory, makeCreditCard, makeDetail, makeTransaction } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionDetailDrawer from '../components/TransactionDetailDrawer.vue'
import TransactionFiltersBar from '../components/TransactionFiltersBar.vue'
import TransactionFormDialog from '../components/TransactionFormDialog.vue'
import TransactionForm from '../components/TransactionForm.vue'
import TransactionList from '../components/TransactionList.vue'
import { defaultFilters } from '../filters'
import { useCreditCardsStore } from '@/stores/creditCards'
import Select from 'primevue/select'

const toastAdd = vi.hoisted(() => vi.fn())
vi.mock('primevue/usetoast', () => ({ useToast: () => ({ add: toastAdd }) }))

const baseProps = { totalCount: 1, page: 1, pageSize: 20, sort: '-transactionDate' }

const installmentTx = makeTransaction({
  id: 'i3',
  description: 'Notebook',
  creditCardId: 'cc1',
  creditCardName: 'Nubank Roxinho',
  installment: { planId: 'p1', number: 3, count: 12 },
})
const occurrenceTx = makeTransaction({ id: 'o1', description: 'Netflix', recurrenceRuleId: 'r1' })

function desktop(): void {
  window.matchMedia = ((query: string) => ({
    matches: true,
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
  })) as unknown as typeof window.matchMedia
}

describe('TransactionList — selos de origem', () => {
  const original = window.matchMedia
  afterEach(() => {
    window.matchMedia = original
  })

  it('mobile: mostra cartão, parcela "3/12" e recorrência como texto (sem links dentro do botão)', () => {
    const { wrapper } = mountWithPlugins(TransactionList, {
      props: { ...baseProps, totalCount: 2, items: [installmentTx, occurrenceTx] } as never,
      pinia: createPinia(),
    })
    const cards = wrapper.findAll('.tx-card')
    expect(cards[0]!.text()).toContain('Nubank Roxinho')
    expect(cards[0]!.find('[data-testid="badge-installment"]').text()).toContain('3/12')
    expect(cards[1]!.find('[data-testid="badge-recurrence"]').text()).toContain('Recorrente')
    expect(wrapper.find('.tx-card a').exists()).toBe(false)
    wrapper.unmount()
  })

  it('desktop: o selo da parcela é um link e um clique real navega para o plano', async () => {
    desktop()
    const { wrapper, router } = mountWithPlugins(TransactionList, {
      props: { ...baseProps, items: [installmentTx] } as never,
      pinia: createPinia(),
    })
    await flushPromises()
    const link = wrapper.find('a[data-testid="badge-installment"]')
    expect(link.attributes('aria-label')).toContain('Parcela 3 de 12')

    await link.trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('installments')
    expect(router.currentRoute.value.query).toEqual({ open: 'p1' })
    // Clicar no link não deve abrir o detalhe da movimentação.
    expect(wrapper.emitted('open')).toBeUndefined()
    wrapper.unmount()
  })

  it('desktop: o selo de recorrência leva à regra com um clique real', async () => {
    desktop()
    const { wrapper, router } = mountWithPlugins(TransactionList, {
      props: { ...baseProps, items: [occurrenceTx] } as never,
      pinia: createPinia(),
    })
    await flushPromises()
    await wrapper.find('a[data-testid="badge-recurrence"]').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('recurrences')
    expect(router.currentRoute.value.query).toEqual({ open: 'r1' })
    wrapper.unmount()
  })
})

describe('TransactionDetailDrawer — parcela e ocorrência', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    toastAdd.mockClear()
  })
  afterEach(() => mock.restore())

  async function open(detail: ReturnType<typeof makeDetail>) {
    mock.onGet(`/transactions/${detail.id}`).reply(200, detail)
    const { wrapper, router } = mountWithPlugins(TransactionDetailDrawer, {
      props: { visible: true, transactionId: detail.id },
      pinia: createPinia(),
    })
    await flushPromises()
    return { wrapper, router }
  }

  const labels = () =>
    Array.from(document.body.querySelectorAll('.tx-drawer__actions button')).map((b) => b.textContent?.trim())

  it('parcela: oculta "Excluir", explica e oferece cancelar; mostra cartão, vencimento da fatura e link do plano', async () => {
    const { wrapper } = await open(
      makeDetail({
        id: 'i3',
        creditCardId: 'cc1',
        creditCardName: 'Nubank Roxinho',
        dueDate: '2026-12-12',
        installment: { planId: 'p1', number: 3, count: 12 },
      }),
    )
    expect(labels()).not.toContain('Excluir')
    expect(labels()).toContain('Cancelar movimentação')
    expect(document.body.querySelector('[data-testid="delete-origin-hint"]')?.textContent).toContain(
      'Parcelas não podem ser excluídas individualmente',
    )
    expect(document.body.querySelector('[data-testid="installment-edit-hint"]')?.textContent).toContain(
      'o valor e a data da movimentação não podem ser alterados',
    )
    expect(document.body.querySelector('[data-testid="detail-card"]')?.textContent).toBe('Nubank Roxinho')
    expect(document.body.textContent).toContain('Vencimento da fatura')
    const link = document.body.querySelector('a[data-testid="badge-installment"]') as HTMLAnchorElement
    expect(link.textContent).toContain('3/12')
    wrapper.unmount()
  })

  it('ocorrência de recorrência: oculta "Excluir" e explica (cancele ou desative a regra)', async () => {
    const { wrapper } = await open(makeDetail({ id: 'o1', recurrenceRuleId: 'r1' }))
    expect(labels()).not.toContain('Excluir')
    expect(document.body.querySelector('[data-testid="delete-origin-hint"]')?.textContent).toContain(
      'desative a regra de recorrência',
    )
    expect(document.body.querySelector('a[data-testid="badge-recurrence"]')).not.toBeNull()
    wrapper.unmount()
  })

  it('movimentação comum continua oferecendo "Excluir"', async () => {
    const { wrapper } = await open(makeDetail({ id: 't9' }))
    expect(labels()).toContain('Excluir')
    expect(document.body.querySelector('[data-testid="delete-origin-hint"]')).toBeNull()
    wrapper.unmount()
  })

  it('422 ao cancelar mostra o `detail` do backend em toast', async () => {
    const detail = makeDetail({ id: 'i3', installment: { planId: 'p1', number: 3, count: 12 } })
    mock.onPost('/transactions/i3/cancel').reply(422, {
      title: 'Regra de negócio violada.',
      detail: 'A parcela não pode ser cancelada.',
      code: 'business_rule_violation',
      status: 422,
    })
    const { wrapper } = await open(detail)
    ;(
      Array.from(document.body.querySelectorAll('button')).find(
        (b) => b.textContent?.trim() === 'Cancelar movimentação',
      ) as HTMLButtonElement
    ).click()
    await flushPromises()
    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({ severity: 'error', detail: 'A parcela não pode ser cancelada.' }),
    )
    wrapper.unmount()
  })
})

describe('TransactionFiltersBar — cartão, plano e regra', () => {
  function setup(filters = defaultFilters()) {
    const pinia = createPinia()
    const { wrapper } = mountWithPlugins(TransactionFiltersBar, { props: { modelValue: filters }, pinia })
    return { wrapper, pinia }
  }

  it('filtro por cartão lista os cartões e emite creditCardId voltando à página 1', async () => {
    const { wrapper, pinia } = setup(defaultFilters({ page: 3 }))
    const cards = useCreditCardsStore(pinia)
    cards.items = [makeCreditCard(), makeCreditCard({ id: 'cc2', name: 'Velho', isActive: false })]
    await flushPromises()

    const select = wrapper.findAllComponents(Select).find((s) => s.props('inputId') === 'filter-card')!
    expect((select.props('options') as { label: string }[]).map((o) => o.label)).toEqual([
      'Todos os cartões',
      'Nubank Roxinho',
      'Velho (inativo)',
    ])
    select.vm.$emit('update:modelValue', 'cc1')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toMatchObject({ creditCardId: 'cc1', page: 1 })
    wrapper.unmount()
  })

  it('mostra e remove os filtros de plano e de regra, contando-os nos filtros ativos', async () => {
    const { wrapper } = setup(defaultFilters({ installmentPlanId: 'p1', recurrenceRuleId: 'r1' }))
    expect(wrapper.text()).toContain('Filtros (2)')
    expect(wrapper.find('[data-testid="filter-plan"]').text()).toContain('parcelas de um plano')
    await wrapper.find('[data-testid="filter-plan"] button').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toMatchObject({
      installmentPlanId: undefined,
      recurrenceRuleId: 'r1',
      page: 1,
    })
    await wrapper.find('[data-testid="filter-rule"] button').trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toMatchObject({ recurrenceRuleId: undefined })
    wrapper.unmount()
  })

  it('"Limpar filtros" remove também cartão, plano e regra', async () => {
    const { wrapper } = setup(defaultFilters({ creditCardId: 'cc1', installmentPlanId: 'p1' }))
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Limpar filtros'))!
      .trigger('click')
    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toEqual({
      page: 1,
      pageSize: 20,
      sort: '-transactionDate',
      accountId: undefined,
    })
    wrapper.unmount()
  })
})

describe('TransactionFormDialog — compra no cartão', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    toastAdd.mockClear()
    mock.onGet('/categories').reply(200, [makeCategory()])
    mock.onGet('/accounts').reply(200, [makeAccount()])
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
  })
  afterEach(() => mock.restore())

  it('após salvar, exibe o vencimento devolvido pela API (sem calcular localmente)', async () => {
    mock
      .onPost('/transactions')
      .reply(
        201,
        makeDetail({ creditCardId: 'cc1', creditCardName: 'Nubank Roxinho', dueDate: '2026-12-12', accountId: null }),
      )
    const { wrapper } = mountWithPlugins(TransactionFormDialog, { props: { visible: true }, pinia: createPinia() })
    await flushPromises()
    wrapper.findComponent(TransactionForm).vm.$emit('submit', {
      description: 'Mercado',
      type: 'Expense',
      amount: 10,
      categoryId: 'c-moradia',
      creditCardId: 'cc1',
      paymentMethod: 'CreditCard',
      transactionDate: '2026-11-18',
      notes: null,
    })
    await flushPromises()

    expect(toastAdd).toHaveBeenCalledWith(
      expect.objectContaining({
        summary: 'Movimentação criada',
        detail: 'Vencimento da fatura: 12/12/2026 (Nubank Roxinho).',
      }),
    )
    wrapper.unmount()
  })

  it('422 de cartão inativo aparece no topo do formulário', async () => {
    mock.onPost('/transactions').reply(422, {
      title: 'Regra de negócio violada.',
      detail: 'O cartão está inativo.',
      code: 'business_rule_violation',
      status: 422,
    })
    const { wrapper } = mountWithPlugins(TransactionFormDialog, { props: { visible: true }, pinia: createPinia() })
    await flushPromises()
    wrapper.findComponent(TransactionForm).vm.$emit('submit', { description: 'x' })
    await flushPromises()
    expect(document.body.querySelector('[data-testid="form-error"]')?.textContent).toContain('O cartão está inativo.')
    wrapper.unmount()
  })
})
