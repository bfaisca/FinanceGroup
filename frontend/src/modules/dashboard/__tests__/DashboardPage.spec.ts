import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { defineComponent } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { useAuthStore } from '@/stores/auth'
import { useClockStore } from '@/stores/clock'
import { makeDashboard } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import DashboardPage from '../pages/DashboardPage.vue'

// ECharts precisa de canvas; aqui só interessam os dados repassados ao gráfico.
const ChartStub = defineComponent({
  name: 'BaseChart',
  props: { option: { type: Object, default: () => ({}) }, ariaLabel: { type: String, default: '' }, empty: Boolean },
  template: '<div class="chart-stub" role="img" :aria-label="ariaLabel">{{ empty ? "vazio" : "grafico" }}</div>',
})

async function mountDashboard(url = '/dashboard') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/dashboard', name: 'dashboard', component: DashboardPage },
      { path: '/accounts', name: 'accounts', component: { template: '<div />' } },
      { path: '/transactions', name: 'transactions', component: { template: '<div />' } },
    ],
  })
  await router.push(url)
  await router.isReady()
  const pinia = createPinia()
  const mounted = mountWithPlugins(DashboardPage, { pinia, router, global: { stubs: { BaseChart: ChartStub } } })
  useAuthStore(pinia).user = { id: 'u1', name: 'Maria Silva', email: 'm@x.com', createdAt: '2026-01-01T00:00:00Z' }
  useClockStore(pinia).today = '2026-11-18'
  await flushPromises()
  return { ...mounted, pinia }
}

describe('DashboardPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('consulta o período do URL e renderiza saldos, totais, gráficos e vencimentos', async () => {
    mock.onGet('/dashboard').reply(200, makeDashboard())
    const { wrapper } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')

    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    const text = wrapper.text()
    expect(text).toContain('Saldo informado')
    expect(text).toContain('Saldo projetado')
    expect(text).toContain('Projetado')
    expect(text).toContain('Projetado em 30/11/2026')
    expect(text).toMatch(/R\$\s5\.000,00/)
    expect(text).toContain('Resultado')
    expect(text).toContain('A pagar')
    expect(text).toContain('novembro de 2026')

    expect(wrapper.findAll('.chart-stub')).toHaveLength(2)
    // Alternativa tabular acessível para a rosca.
    expect(wrapper.find('table').text()).toContain('Moradia')
    expect(wrapper.text()).toContain('75,0%')
    wrapper.unmount()
  })

  it('destaca vencimentos atrasados e abre o detalhe ao clicar', async () => {
    mock.onGet('/dashboard').reply(200, makeDashboard())
    const { wrapper, router } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')

    const items = wrapper.findAll('.dues__item')
    expect(items).toHaveLength(2)
    expect(items[0]!.classes()).toContain('dues__item--overdue')
    expect(items[0]!.text()).toContain('Atrasado')
    expect(items[0]!.text()).toContain('Venceu há 3 dias')

    await items[0]!.trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('transactions')
    expect(router.currentRoute.value.query.open).toBe('late')
    wrapper.unmount()
  })

  it('avisa quando há contas sem saldo informado', async () => {
    mock.onGet('/dashboard').reply(
      200,
      makeDashboard({
        balance: {
          reportedTotal: 1000,
          projectedTotal: 900,
          projectedAsOf: '2026-11-30',
          accountsWithBalance: 1,
          accountsWithoutBalance: 2,
        },
      }),
    )
    const { wrapper } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')
    expect(wrapper.find('[data-testid="balance-warning"]').text()).toContain(
      '2 contas sem saldo informado ficam fora dos totais',
    )
    wrapper.unmount()
  })

  it('sem saldo informado: cartões não inventam valor', async () => {
    mock.onGet('/dashboard').reply(
      200,
      makeDashboard({
        balance: {
          reportedTotal: null,
          projectedTotal: null,
          projectedAsOf: '2026-11-30',
          accountsWithBalance: 0,
          accountsWithoutBalance: 1,
        },
      }),
    )
    const { wrapper } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')
    expect(wrapper.text()).toContain('Nenhuma conta com saldo informado')
    expect(wrapper.text()).toContain('—')
    wrapper.unmount()
  })

  it('estado vazio guia a criar conta e movimentação', async () => {
    mock.onGet('/dashboard').reply(
      200,
      makeDashboard({
        balance: {
          reportedTotal: null,
          projectedTotal: null,
          projectedAsOf: '2026-11-30',
          accountsWithBalance: 0,
          accountsWithoutBalance: 0,
        },
        totals: {
          income: 0,
          expense: 0,
          result: 0,
          receivedIncome: 0,
          pendingIncome: 0,
          paidExpense: 0,
          pendingExpense: 0,
        },
        expensesByCategory: [],
        upcomingDues: [],
        monthlyHistory: [],
      }),
    )
    const { wrapper } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')
    expect(wrapper.text()).toContain('Comece cadastrando suas finanças')
    const labels = wrapper.findAll('button').map((b) => b.text())
    expect(labels).toEqual(expect.arrayContaining(['Criar conta', 'Registrar movimentação']))
    expect(wrapper.find('.chart-stub').exists()).toBe(false)
    wrapper.unmount()
  })

  it('mostra erro com "Tentar novamente" e se recupera', async () => {
    mock.onGet('/dashboard').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/dashboard').reply(200, makeDashboard())
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.find('[role="alert"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Saldo projetado')
    wrapper.unmount()
  })

  it('mostra carregando na primeira carga', async () => {
    mock.onGet('/dashboard').reply(() => new Promise(() => undefined))
    const { wrapper } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')
    expect(wrapper.text()).toContain('Carregando seu resumo')
    wrapper.unmount()
  })

  it('navegar para o mês anterior consulta o novo período e atualiza a URL', async () => {
    mock.onGet('/dashboard').reply(200, makeDashboard())
    const { wrapper, router } = await mountDashboard('/dashboard?from=2026-11-01&to=2026-11-30')

    await wrapper.find('button[aria-label="Mês anterior"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ from: '2026-10-01', to: '2026-10-31' })
    expect(mock.history.get.at(-1)?.params).toEqual({ from: '2026-10-01', to: '2026-10-31' })
    wrapper.unmount()
  })

  it('usa o mês do "hoje" quando a URL não traz período e adota o `today` do servidor', async () => {
    mock.onGet('/dashboard').reply(200, makeDashboard())
    const { wrapper, pinia } = await mountDashboard('/dashboard')
    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    expect(useClockStore(pinia).fromServer).toBe(true)
    wrapper.unmount()
  })
})
