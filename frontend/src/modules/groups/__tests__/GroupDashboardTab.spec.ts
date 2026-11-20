import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import GroupDashboardTab from '../components/GroupDashboardTab.vue'
import { NOT_DEBT_NOTICE } from '../dashboard'
import { makeGroupDashboard, makeGroupDetail } from './fixtures'
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

  it('consulta o período da URL e mostra totais, pago/pendente e saldo', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30' })
    const text = wrapper.text()
    for (const label of [
      'Despesas',
      'Receitas',
      'Saldo do período',
      'Total pago',
      'Total pendente',
      'Recebido',
      'A receber',
    ]) {
      expect(text).toContain(label)
    }
    expect(text).toMatch(/R\$\s1\.000,00/)
    expect(text).toMatch(/R\$\s2\.000,00/)
    expect(text).toContain('novembro de 2026')
    wrapper.unmount()
  })

  it('usa o mês atual por padrão e envia `months` só quando diferente de 6', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab('/groups/g1?months=12')

    expect(mock.history.get[0]?.params).toEqual({ from: '2026-11-01', to: '2026-11-30', months: 12 })
    wrapper.unmount()
  })

  it('exibe DUAS participações separadas, cada uma com título e regra de cálculo próprios', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    const created = wrapper.find('section[aria-labelledby="participation-createdBy-title"]')
    const paid = wrapper.find('section[aria-labelledby="participation-paidBy-title"]')
    expect(created.exists()).toBe(true)
    expect(paid.exists()).toBe(true)
    expect(created.find('h3').text()).toBe('Participação por quem cadastrou')
    expect(paid.find('h3').text()).toBe('Participação por quem pagou')

    const createdRule = created.find('[data-testid="participation-rule"]').text()
    const paidRule = paid.find('[data-testid="participation-rule"]').text()
    expect(createdRule).toContain('cadastradas por cada participante')
    expect(createdRule).toContain('tenha ela sido paga ou não')
    expect(paidRule).toContain('pagamentos e recebimentos efetivamente registrados')
    expect(paidRule).toContain('Movimentações pendentes não entram')
    expect(createdRule).not.toBe(paidRule)

    // Cada gráfico tem a própria alternativa em tabela, com dados distintos.
    expect(created.text()).toContain('Ver dados em tabela')
    expect(paid.text()).toContain('Ver dados em tabela')
    expect(created.find('table').text()).toContain('Maria Silva')
    expect(paid.find('table').text()).toContain('João Souza')
    expect(paid.find('table').text()).not.toContain('Maria Silva')
    wrapper.unmount()
  })

  it('marca ex-membros como "(ex-membro)" nas participações', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    const created = wrapper.find('section[aria-labelledby="participation-createdBy-title"]')
    expect(created.find('table').text()).toContain('Carlos Antigo (ex-membro)')
    expect(created.find('[role="img"]').attributes('aria-label')).toContain('Carlos Antigo (ex-membro)')
    expect(created.find('table').text()).not.toContain('Maria Silva (ex-membro)')
    wrapper.unmount()
  })

  it('mostra o aviso fixo de que percentuais não são dívida', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    const notice = wrapper.find('[data-testid="not-debt-notice"]')
    // Aviso estático: não é anunciado como alerta assertivo.
    expect(notice.find('.p-message').attributes('role')).toBe('note')
    expect(notice.text()).toContain('indicadores estatísticos')
    expect(notice.text()).toContain('NÃO representam dívida')
    expect(notice.text()).toContain(NOT_DEBT_NOTICE)
    wrapper.unmount()
  })

  it('o aviso fixo aparece mesmo com o período vazio', async () => {
    mock.onGet('/groups/g1/dashboard').reply(
      200,
      makeGroupDashboard({
        totals: {
          expenses: 0,
          expensesPaid: 0,
          expensesPending: 0,
          income: 0,
          incomeReceived: 0,
          incomePending: 0,
          balance: 0,
        },
        createdBy: [],
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

  it('evolução mensal e comparação com o período anterior (base zero vira "—")', async () => {
    mock.onGet('/groups/g1/dashboard').reply(200, makeGroupDashboard())
    const { wrapper } = await mountTab()

    expect(wrapper.find('[data-testid="group-monthly-table"]').text()).toContain('novembro de 2026')
    expect(wrapper.find('[data-testid="expenses-change"]').text()).toBe('+25,0%')
    expect(wrapper.find('[data-testid="income-change"]').text()).toBe('—')
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
    expect(wrapper.text()).toContain('Participação por quem cadastrou')
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
