import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { makeAccount, makeCategory, makeCreditCard, makePlan, makePlanDetail } from '@/test/fixtures'
import { ChartStub, mountWithPlugins, useDesktopViewport } from '@/test/mount'
import type { ReportInstallmentsDto } from '@/types/api'
import InstallmentsPage from '../pages/InstallmentsPage.vue'

function paged(items = [makePlan()], totalCount = items.length) {
  return { items, page: 1, pageSize: 10, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 10)) }
}

const commitment: ReportInstallmentsDto = {
  openPlans: 1,
  totalRemaining: 2500,
  commitmentByMonth: [{ month: '2026-12', amount: 250, installmentsCount: 1 }],
  plans: [],
}

async function mountAt(url: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/installments', name: 'installments', component: InstallmentsPage }],
  })
  await router.push(url)
  await router.isReady()
  const { wrapper } = mountWithPlugins(InstallmentsPage, {
    pinia: createPinia(),
    router,
    global: { stubs: { BaseChart: ChartStub } },
  })
  await flushPromises()
  return { wrapper, router }
}

describe('InstallmentsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory({ id: 'c-compras', name: 'Compras' })])
    mock.onGet('/accounts').reply(200, [makeAccount()])
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    mock.onGet('/reports/installments').reply(200, commitment)
  })
  afterEach(() => {
    mock.restore()
  })

  it('lista os planos (paginada) e mostra o comprometimento futuro com tabela alternativa', async () => {
    mock.onGet('/installment-plans').reply(200, paged())
    const { wrapper } = await mountAt('/installments')

    const request = mock.history.get.find((r) => r.url === '/installment-plans')!
    expect(request.params).toEqual({ page: 1, pageSize: 10 })
    expect(wrapper.text()).toContain('Notebook')
    expect(wrapper.text()).toContain('Comprometimento futuro')
    expect(wrapper.find('.chart-stub').attributes('aria-label')).toContain('dezembro de 2026')
    expect(wrapper.find('[data-testid="commitment-table"]').exists()).toBe(true)
    expect(mock.history.get.find((r) => r.url === '/reports/installments')?.params).toEqual({ months: 12 })
    wrapper.unmount()
  })

  it('o filtro de situação vem da URL e altera a consulta e a URL', async () => {
    mock.onGet('/installment-plans').reply(200, paged())
    const { wrapper, router } = await mountAt('/installments?status=Completed&page=2')
    const first = mock.history.get.find((r) => r.url === '/installment-plans')!
    expect(first.params).toEqual({ page: 2, pageSize: 10, status: 'Completed' })

    const select = wrapper.findComponent({ name: 'Select' })
    select.vm.$emit('update:modelValue', 'Open')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ status: 'Open' })
    expect(mock.history.get.filter((r) => r.url === '/installment-plans').at(-1)?.params).toEqual({
      page: 1,
      pageSize: 10,
      status: 'Open',
    })
    wrapper.unmount()
  })

  it('estado vazio, erro com retry e carregamento', async () => {
    mock.onGet('/installment-plans').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountAt('/installments')
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/installment-plans').reply(200, paged([], 0))
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Nenhum parcelamento ainda')
    wrapper.unmount()
  })

  it('sem planos em aberto, o comprometimento mostra estado vazio', async () => {
    mock.onGet('/installment-plans').reply(200, paged([], 0))
    mock.onGet('/reports/installments').reply(200, { ...commitment, openPlans: 0, totalRemaining: 0 })
    const { wrapper } = await mountAt('/installments')
    expect(wrapper.text()).toContain('Nenhum parcelamento em aberto')
    wrapper.unmount()
  })

  it('erro no comprometimento aparece com "Tentar novamente" independente da lista', async () => {
    mock.resetHandlers()
    mock.onGet('/categories').reply(200, [])
    mock.onGet('/accounts').reply(200, [])
    mock.onGet('/credit-cards').reply(200, [])
    mock.onGet('/installment-plans').reply(200, paged())
    mock.onGet('/reports/installments').reply(500, { title: 'Falha no relatório.' })
    const { wrapper } = await mountAt('/installments')
    expect(wrapper.text()).toContain('Notebook')
    expect(wrapper.text()).toContain('Falha no relatório.')
    wrapper.unmount()
  })

  it('abrir um plano põe `open` na URL e carrega o detalhe', async () => {
    mock.onGet('/installment-plans').reply(200, paged())
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    const { wrapper, router } = await mountAt('/installments')

    await wrapper.find('button.plan-card').trigger('click')
    await flushPromises()
    expect(router.currentRoute.value.query).toEqual({ open: 'p1' })
    expect(document.body.querySelector('[data-testid="plan-summary"]')).not.toBeNull()
    wrapper.unmount()
  })

  it('`?open=<id>` abre o detalhe direto', async () => {
    const restore = useDesktopViewport()
    mock.onGet('/installment-plans').reply(200, paged())
    mock.onGet('/installment-plans/p1').reply(200, makePlanDetail())
    const { wrapper } = await mountAt('/installments?open=p1')
    expect(document.body.querySelector('[data-testid="installments-table"]')).not.toBeNull()
    wrapper.unmount()
    restore()
  })
})
