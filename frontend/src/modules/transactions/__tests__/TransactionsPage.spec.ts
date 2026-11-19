import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { makeAccount, makeCategory, makeDetail, makeTransaction } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionsPage from '../pages/TransactionsPage.vue'

function pageOf(items = [makeTransaction()], totalCount = items.length) {
  return { items, page: 1, pageSize: 20, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 20)) }
}

async function mountAt(url: string) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/transactions', name: 'transactions', component: TransactionsPage }],
  })
  await router.push(url)
  await router.isReady()
  const { wrapper } = mountWithPlugins(TransactionsPage, { pinia: createPinia(), router })
  await flushPromises()
  return { wrapper, router }
}

describe('TransactionsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory()])
    mock.onGet('/accounts').reply(200, [makeAccount()])
  })
  afterEach(() => mock.restore())

  it('lê os filtros da URL e os envia ao backend', async () => {
    mock.onGet('/transactions').reply(200, pageOf())
    const { wrapper } = await mountAt(
      '/transactions?status=Overdue&accountId=a1&from=2026-11-01&to=2026-11-30&search=luz&page=2&sort=-amount',
    )

    const request = mock.history.get.find((r) => r.url === '/transactions')!
    expect(request.params).toEqual({
      page: 2,
      pageSize: 20,
      sort: '-amount',
      status: 'Overdue',
      accountId: 'a1',
      from: '2026-11-01',
      to: '2026-11-30',
      search: 'luz',
    })
    expect(wrapper.text()).toContain('Aluguel')
    wrapper.unmount()
  })

  it('alterar a busca atualiza a URL (sem `page`) e recarrega a lista', async () => {
    mock.onGet('/transactions').reply(200, pageOf())
    const { wrapper, router } = await mountAt('/transactions?page=3')

    const input = wrapper.find('input[name="search"]')
    await input.setValue('mercado')
    await input.trigger('keydown', { key: 'Enter' })
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ search: 'mercado' })
    const last = mock.history.get.filter((r) => r.url === '/transactions').at(-1)!
    expect(last.params).toMatchObject({ search: 'mercado', page: 1 })
    wrapper.unmount()
  })

  it('mostra erro e permite tentar novamente', async () => {
    mock.onGet('/transactions').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountAt('/transactions')
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/transactions').reply(200, pageOf())
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Aluguel')
    wrapper.unmount()
  })

  it('abre o detalhe quando `?open=<id>` está na URL e mostra ações por estado', async () => {
    mock.onGet('/transactions').reply(200, pageOf())
    mock.onGet('/transactions/t1').reply(200, makeDetail({ paidAmount: 500, remainingAmount: 1000 }))
    const { wrapper } = await mountAt('/transactions?open=t1')

    const text = document.body.textContent ?? ''
    expect(text).toContain('Registrar pagamento')
    expect(text).toContain('Cancelar movimentação')
    // Com pagamentos: não oferece excluir e explica o motivo.
    expect(document.body.querySelector('[data-testid="delete-hint"]')?.textContent).toContain('não é possível excluir')
    expect(Array.from(document.body.querySelectorAll('button')).some((b) => b.textContent?.trim() === 'Excluir')).toBe(
      false,
    )
    wrapper.unmount()
  })

  it('movimentação cancelada oferece apenas Reabrir', async () => {
    mock.onGet('/transactions').reply(200, pageOf())
    mock.onGet('/transactions/t1').reply(200, makeDetail({ status: 'Canceled' }))
    const { wrapper } = await mountAt('/transactions?open=t1')

    const labels = Array.from(document.body.querySelectorAll('.tx-drawer__actions button')).map((b) =>
      b.textContent?.trim(),
    )
    expect(labels).toContain('Reabrir')
    expect(labels).not.toContain('Registrar pagamento')
    expect(labels).not.toContain('Editar')
    wrapper.unmount()
  })

  it('`?new=1` abre o formulário e remove o parâmetro', async () => {
    mock.onGet('/transactions').reply(200, pageOf())
    const { wrapper, router } = await mountAt('/transactions?new=1')
    await flushPromises()
    expect(router.currentRoute.value.query.new).toBeUndefined()
    expect(document.body.textContent).toContain('Nova movimentação')
    expect(document.body.querySelector('form[aria-label="Nova movimentação"]')).not.toBeNull()
    wrapper.unmount()
  })
})
