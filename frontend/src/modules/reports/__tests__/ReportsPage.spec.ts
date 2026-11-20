import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { useClockStore } from '@/stores/clock'
import { makePlan } from '@/test/fixtures'
import { ChartStub, mountWithPlugins } from '@/test/mount'
import ReportsPage from '../pages/ReportsPage.vue'

const summary = {
  period: { from: '2026-11-01', to: '2026-11-30' },
  totals: {
    income: 5000,
    expense: 1200,
    result: 3800,
    receivedIncome: 5000,
    pendingIncome: 0,
    paidExpense: 1000,
    pendingExpense: 200,
  },
  series: [
    { key: '2026-11-01', income: 5000, expense: 200, result: 4800 },
    { key: '2026-11-02', income: 0, expense: 1000, result: -1000 },
  ],
}

async function settled(wrapper: { find: (s: string) => { exists: () => boolean } }) {
  await vi.waitFor(async () => {
    await flushPromises()
    expect(wrapper.find('[role="tabpanel"] .loading-state, [aria-busy="true"]').exists()).toBe(false)
  })
}

async function mountAt(url = '/reports') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/reports', name: 'reports', component: ReportsPage },
      { path: '/installments', name: 'installments', component: { template: '<div />' } },
      { path: '/recurrences', name: 'recurrences', component: { template: '<div />' } },
    ],
  })
  await router.push(url)
  await router.isReady()
  const pinia = createPinia()
  const { wrapper } = mountWithPlugins(ReportsPage, {
    pinia,
    router,
    global: { stubs: { BaseChart: ChartStub } },
  })
  // As abas são componentes assíncronos (import dinâmico): aguarda sair do estado de carregamento.
  await settled(wrapper)
  return { wrapper, router }
}

function gets(mock: MockAdapter, url: string) {
  return mock.history.get.filter((r) => r.url === url)
}

describe('ReportsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/reports/summary').reply(200, summary)
  })
  afterEach(() => mock.restore())

  it('abre em Resumo, usa o mês atual e carrega somente a aba ativa (lazy)', async () => {
    const { wrapper } = await mountAt()
    const today = useClockStore().today
    expect(gets(mock, '/reports/summary')).toHaveLength(1)
    expect(gets(mock, '/reports/summary')[0]!.params.from).toBe(`${today.slice(0, 7)}-01`)
    expect(mock.history.get.map((r) => r.url)).toEqual(['/reports/summary'])
    expect(wrapper.findAll('[role="tab"]').map((t) => t.text())).toEqual([
      'Resumo',
      'Por categoria',
      'Por conta',
      'Por método de pagamento',
      'Evolução mensal',
      'Parcelamentos',
      'Recorrências',
    ])
    expect(wrapper.find('[data-testid="summary-report"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="summary-table"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Relatórios de grupos chegam com a etapa de Grupos.')
    wrapper.unmount()
  })

  it('período da URL vira parâmetros; período > 366 dias volta ao padrão', async () => {
    const ok = await mountAt('/reports?from=2026-01-01&to=2026-03-31')
    expect(gets(mock, '/reports/summary')[0]!.params).toMatchObject({
      from: '2026-01-01',
      to: '2026-03-31',
      groupBy: 'month',
    })
    ok.wrapper.unmount()

    mock.resetHistory()
    const bad = await mountAt('/reports?from=2024-01-01&to=2026-03-31')
    expect(gets(mock, '/reports/summary')[0]!.params.from).not.toBe('2024-01-01')
    bad.wrapper.unmount()
  })

  it('trocar de aba atualiza ?tab e carrega o relatório correspondente', async () => {
    mock.onGet('/reports/by-category').reply(200, {
      period: summary.period,
      type: 'Expense',
      total: 1200,
      items: [
        {
          categoryId: 'c1',
          categoryName: 'Alimentação',
          color: null,
          total: 1200,
          percentage: 100,
          subcategories: [{ categoryId: 's1', categoryName: 'Mercado', total: 700, percentage: 58.33 }],
        },
      ],
    })
    const { wrapper, router } = await mountAt()
    await wrapper
      .findAll('[role="tab"]')
      .find((t) => t.text() === 'Por categoria')!
      .trigger('click')
    await settled(wrapper)
    expect(router.currentRoute.value.query.tab).toBe('categories')
    expect(gets(mock, '/reports/by-category')[0]!.params).toMatchObject({ type: 'Expense' })
    const table = wrapper.find('[data-testid="category-table"]')
    expect(table.exists()).toBe(true)
    expect(table.text()).toContain('Alimentação')
    // Subcategorias aparecem ao expandir.
    expect(wrapper.findAll('[data-testid="subcategory-row"]')).toHaveLength(0)
    await table.find('button[aria-label^="Expandir subcategorias"]').trigger('click')
    expect(wrapper.findAll('[data-testid="subcategory-row"]')[0]!.text()).toContain('Mercado')
    wrapper.unmount()
  })

  it('Evolução mensal: months da URL vai para a API; mudar a janela atualiza ?months', async () => {
    mock.onGet('/reports/monthly-evolution').reply(200, {
      items: [
        { month: '2026-10', income: 100, expense: 50, result: 50, cumulativeResult: 50 },
        { month: '2026-11', income: 100, expense: 150, result: -50, cumulativeResult: 0 },
      ],
    })
    const { wrapper, router } = await mountAt('/reports?tab=evolution&months=24')
    expect(gets(mock, '/reports/monthly-evolution')[0]!.params).toEqual({ months: 24 })
    expect(wrapper.find('[data-testid="evolution-table"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('A janela em meses usa o mês atual como referência.')
    // A aba de evolução não exibe o filtro de período.
    expect(wrapper.find('input[aria-label*="Início"]').exists()).toBe(false)

    const six = wrapper
      .findAll(
        '[aria-label="Meses da evolução"] [role="button"], [aria-label="Meses da evolução"] button, [aria-label="Meses da evolução"] .p-togglebutton',
      )
      .find((b) => b.text() === '6 meses')!
    await six.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query.months).toBe('6')
    wrapper.unmount()
  })

  it('Parcelamentos: lista planos abertos e comprometimento por mês em tabela alternativa', async () => {
    mock.onGet('/reports/installments').reply(200, {
      openPlans: 1,
      totalRemaining: 2500,
      commitmentByMonth: [{ month: '2026-12', amount: 250, installmentsCount: 1 }],
      plans: [makePlan()],
    })
    const { wrapper } = await mountAt('/reports?tab=installments')
    expect(gets(mock, '/reports/installments')[0]!.params).toEqual({ months: 12 })
    const table = wrapper.find('[data-testid="open-plans-table"]')
    expect(table.text()).toContain('Notebook')
    expect(wrapper.text()).toMatch(/R\$\s*2\.500,00/)
    expect(wrapper.find('details').exists()).toBe(true)
    wrapper.unmount()
  })

  it('Recorrências: regras ativas e próximos meses', async () => {
    mock.onGet('/reports/recurrences').reply(200, {
      activeRules: 2,
      upcomingByMonth: [{ month: '2026-12', income: 0, expense: 55.9 }],
      rules: [
        {
          id: 'r1',
          description: 'Netflix',
          type: 'Expense',
          amount: 55.9,
          frequency: 'Monthly',
          interval: 1,
          nextOccurrenceDate: '2026-12-10',
          upcomingTotal12m: 670.8,
        },
      ],
    })
    const { wrapper } = await mountAt('/reports?tab=recurrences')
    expect(wrapper.find('[data-testid="active-rules"]').text()).toBe('2')
    expect(wrapper.find('[data-testid="rules-table"]').text()).toContain('Netflix')
    expect(wrapper.find('[data-testid="rules-table"]').text()).toContain('Mensal')
    expect(wrapper.find('[data-testid="upcoming-table"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('erro mostra alerta e "Tentar novamente" recarrega', async () => {
    mock.reset()
    mock.onGet('/reports/by-account').replyOnce(500, { title: 'Falha no relatório.' })
    const { wrapper } = await mountAt('/reports?tab=accounts')
    expect(wrapper.find('[data-testid="report-error"]').text()).toContain('Falha no relatório.')

    mock.onGet('/reports/by-account').reply(200, {
      period: summary.period,
      type: 'Expense',
      total: 100,
      items: [{ accountId: null, accountName: 'Sem conta', total: 100, percentage: 100 }],
    })
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.find('[data-testid="share-table"]').text()).toContain('Sem conta')
    expect(wrapper.find('[data-testid="report-error"]').exists()).toBe(false)
    wrapper.unmount()
  })

  it('sem dados mostra estado vazio', async () => {
    mock.reset()
    mock
      .onGet('/reports/by-payment-method')
      .reply(200, { period: summary.period, type: 'Expense', total: 0, items: [] })
    const { wrapper } = await mountAt('/reports?tab=methods')
    expect(wrapper.text()).toContain('Nenhuma movimentação no período')
    wrapper.unmount()
  })

  it('por método: item sem método aparece como "Não informado"', async () => {
    mock.reset()
    mock.onGet('/reports/by-payment-method').reply(200, {
      period: summary.period,
      type: 'Expense',
      total: 100,
      items: [
        { paymentMethod: null, total: 40, percentage: 40 },
        { paymentMethod: 'CreditCard', total: 60, percentage: 60 },
      ],
    })
    const { wrapper } = await mountAt('/reports?tab=methods')
    const text = wrapper.find('[data-testid="share-table"]').text()
    expect(text).toContain('Não informado')
    expect(text).toContain('Cartão de crédito')
    wrapper.unmount()
  })
})
