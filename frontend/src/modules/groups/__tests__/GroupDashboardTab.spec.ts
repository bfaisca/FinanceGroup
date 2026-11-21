import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupDashboardTab from '../components/GroupDashboardTab.vue'
import { NOT_DEBT_NOTICE } from '../dashboard'
import { makeGroupDashboard, makeGroupDetail, makePaidBy } from './fixtures'
import { button, mountAt } from './helpers'

const mountTab = (url = '/groups/g1?from=2026-11-01&to=2026-11-30') =>
  mountAt(
    GroupDashboardTab,
    url,
    { path: '/groups/:id', name: 'group-detail' },
    { props: { group: makeGroupDetail() } },
  )

describe('GroupDashboardTab', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
  })
  afterEach(() => mock.restore())

  it('consulta o período da URL e mostra os 4 totais (despesas, pago, pendente, juros) sem receitas', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    const summary = wrapper.find('section[aria-label="Resumo do período"]')
    const text = summary.text()
    for (const label of ['Total de despesas', 'Pago', 'Pendente', 'Juros/multa']) expect(text).toContain(label)
    expect(text).toMatch(/R\$\s1\.000,00/)
    expect(text).toMatch(/R\$\s423,50/)
    expect(text).toMatch(/R\$\s600,00/)
    expect(text).toMatch(/R\$\s23,50/)
    const all = wrapper.text()
    for (const gone of ['Receitas', 'Saldo do período', 'Recebido', 'A receber']) expect(all).not.toContain(gone)
    expect(all).toContain('novembro de 2026')
    wrapper.unmount()
  })

  it('usa o mês atual por padrão e envia `months` só quando diferente de 6', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab('/groups/g1?months=12')

    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30', months: 12 })
    wrapper.unmount()
  })

  it('não tem mais "Participação por quem cadastrou"', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()
    expect(wrapper.text()).not.toContain('quem cadastrou')
    expect(wrapper.text()).not.toContain('Participação por')
    wrapper.unmount()
  })

  it('gráfico Pago × Pendente usa expensesPaid e expensesPending e destaca os juros à parte', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    const card = wrapper.find('[data-testid="paid-pending-card"]')
    expect(card.find('h3').text()).toBe('Pago × Pendente')
    expect(card.find('[data-testid="slice-paid"]').text()).toMatch(/Pago\s*R\$\s423,50/)
    expect(card.find('[data-testid="slice-pending"]').text()).toMatch(/Pendente\s*R\$\s600,00/)
    expect(card.find('[data-testid="interest-line"]').text()).toMatch(/R\$\s23,50/)
    expect(card.find('[role="img"]').attributes('aria-label')).toContain('Pago: R$')
    expect(card.text()).toContain('Ver dados em tabela')
    expect(card.find('[data-testid="paid-pending-table"]').text()).toContain('Juros/multa pagos')
    wrapper.unmount()
  })

  it('"Contas Pagas" lista quem pagou com valor total, percentual e juros em destaque, sem regra longa', async () => {
    mock.onGet('/groups/g1/dashboard').reply(
      200,
      makeGroupDashboard({
        paidBy: [
          makePaidBy({ userId: 'u2', name: 'João Souza', paidAmount: 323.5, interestAmount: 23.5, percent: 76.38 }),
          makePaidBy({
            userId: 'u9',
            name: 'Carlos Antigo',
            status: 'Left',
            paidAmount: 100,
            interestAmount: 0,
            percent: 23.62,
          }),
        ],
      }),
    )
    const { wrapper } = await mountTab()

    const card = wrapper.find('[data-testid="paid-by-card"]')
    expect(card.find('h3').text()).toBe('Contas Pagas')
    expect(card.text()).toContain('Quanto cada pessoa pagou (inclui juros/multa).')
    expect(card.text()).not.toContain('Regra de cálculo')

    const items = card.findAll('[data-testid="paid-by-item"]')
    expect(items).toHaveLength(2)
    expect(items[0]!.text()).toContain('João Souza')
    expect(items[0]!.text()).toMatch(/R\$\s323,50/)
    expect(items[0]!.text()).toContain('76,4%')
    expect(items[0]!.text()).toMatch(/inclui R\$\s23,50 de juros\/multa/)
    expect(items[1]!.text()).toContain('Carlos Antigo (ex-membro)')
    expect(items[1]!.text()).not.toContain('juros')

    const table = card.find('[data-testid="paid-by-table"]')
    expect(table.text()).toContain('Total pago')
    expect(table.text()).toMatch(/R\$\s300,00/)
    expect(card.text()).toContain('Ver dados em tabela')
    wrapper.unmount()
  })

  it('mostra o aviso curto de que percentuais não são dívida', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    const notice = wrapper.find('[data-testid="not-debt-notice"]')
    // Aviso estático: não é anunciado como alerta assertivo.
    expect(notice.find('.p-message').attributes('role')).toBe('note')
    expect(notice.text()).toContain('indicadores')
    expect(notice.text()).toContain('não representam dívida')
    expect(notice.text()).toContain(NOT_DEBT_NOTICE)
    wrapper.unmount()
  })

  it('o aviso fixo aparece mesmo com o período vazio', async () => {
    mock.onGet('/groups/g1/dashboard').reply(
      200,
      makeGroupDashboard({
        totals: { expenses: 0, expensesPaidPrincipal: 0, interestPaid: 0, expensesPaid: 0, expensesPending: 0 },
        paidBy: [],
        monthly: [],
      }),
    )
    const { wrapper } = await mountTab()
    expect(wrapper.text()).toContain('Sem movimentações neste período')
    expect(wrapper.find('[data-testid="not-debt-notice"]').exists()).toBe(true)
    wrapper.unmount()
  })

  it('despesas por categoria expandem subcategorias na tabela', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    expect(wrapper.findAll('[data-testid="subcategory-row"]')).toHaveLength(0)
    await wrapper.find('button[aria-label="Expandir subcategorias de Moradia"]').trigger('click')
    expect(wrapper.find('[data-testid="subcategory-row"]').text()).toContain('Condomínio')
    wrapper.unmount()
  })

  it('evolução mensal (despesas e pago) e comparação com o período anterior (base zero vira "—")', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    const monthly = wrapper.find('[data-testid="group-monthly-table"]')
    expect(monthly.text()).toContain('novembro de 2026')
    expect(monthly.text()).toMatch(/Despesas\s*Pago/)
    expect(monthly.text()).toMatch(/R\$\s1\.000,00\s*R\$\s423,50/)
    expect(wrapper.find('[data-testid="expenses-change"]').text()).toBe('+25,0%')
    expect(wrapper.find('[data-testid="income-change"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('01/10/2026 a 31/10/2026')
    wrapper.unmount()
  })

  it('mostra carregamento, erro com nova tentativa e sucesso', async () => {
    mock.onGet('/groups/g1/dashboard').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountTab()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    await button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.text()).toContain('Contas Pagas')
    wrapper.unmount()
  })

  it('trocar o período pela navegação recarrega com as novas datas', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper, router } = await mountTab()

    await wrapper.find('button[aria-label="Mês anterior"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.query).toMatchObject({ from: '2026-10-01', to: '2026-10-31' })
    expect(mock.history.get.at(-1)?.params).toEqual({ from: '2026-10-01', to: '2026-10-31' })
    wrapper.unmount()
  })
})
