import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { makeAccount, makeCategory, makeTransaction } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import AccountDetailPage from '../pages/AccountDetailPage.vue'

async function mountAt(id = 'a1') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/accounts', name: 'accounts', component: { template: '<div />' } },
      { path: '/accounts/:id', name: 'account-detail', component: AccountDetailPage },
      { path: '/transactions', name: 'transactions', component: { template: '<div />' } },
    ],
  })
  await router.push(`/accounts/${id}`)
  await router.isReady()
  const { wrapper } = mountWithPlugins(AccountDetailPage, { pinia: createPinia(), router })
  await flushPromises()
  return wrapper
}

describe('AccountDetailPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory()])
    mock.onGet('/accounts').reply(200, [makeAccount()])
    mock.onGet('/accounts/a1').reply(200, makeAccount())
    mock.onGet('/accounts/a1/balance-snapshots').reply(200, [
      { id: 's2', balance: 1000, referenceDate: '2026-11-01', createdAt: '2026-11-01T10:00:00Z' },
      { id: 's1', balance: 500, referenceDate: '2026-10-01', createdAt: '2026-10-01T10:00:00Z' },
    ])
    mock.onGet('/transactions').reply(200, {
      items: [makeTransaction({ accountId: 'a1', accountName: 'Nubank' })],
      page: 1,
      pageSize: 10,
      totalCount: 1,
      totalPages: 1,
    })
  })
  afterEach(() => mock.restore())

  it('mostra saldos, histórico de saldos e movimentações filtradas por conta', async () => {
    const wrapper = await mountAt()

    expect(wrapper.text()).toContain('Saldo projetado')
    expect(wrapper.text()).toContain('Não é o saldo bancário')
    const rows = wrapper.findAll('table tbody tr')
    expect(rows).toHaveLength(2)
    expect(rows[0]!.text()).toContain('01/11/2026')

    const tx = mock.history.get.find((r) => r.url === '/transactions')!
    expect(tx.params).toMatchObject({ accountId: 'a1', pageSize: 10 })
    expect(wrapper.text()).toContain('Aluguel')
    expect(wrapper.find('a[href*="accountId=a1"]').text()).toBe('Ver todas')
    wrapper.unmount()
  })

  it('"Informar novo saldo" envia o snapshot e recarrega a conta', async () => {
    mock.onPost('/accounts/a1/balance-snapshots').reply(201, { id: 's3' })
    const wrapper = await mountAt()

    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Informar novo saldo'))!
      .trigger('click')
    await flushPromises()
    const balance = document.body.querySelector('#snap-balance') as HTMLInputElement
    balance.value = '1.250,75'
    balance.dispatchEvent(new Event('input'))
    await flushPromises()
    ;(
      document.body.querySelector('form[aria-label="Informar novo saldo"] button[type="submit"]') as HTMLButtonElement
    ).click()
    await flushPromises()

    expect(JSON.parse(mock.history.post[0]!.data as string)).toMatchObject({ balance: 1250.75 })
    expect(mock.history.get.filter((r) => r.url === '/accounts/a1').length).toBeGreaterThanOrEqual(2)
    wrapper.unmount()
  })

  it('conta inativa: botão de novo saldo desativado e aviso explicativo', async () => {
    mock.onGet('/accounts/a1').reply(200, makeAccount({ isActive: false }))
    const wrapper = await mountAt()
    const button = wrapper.findAll('button').find((b) => b.text().includes('Informar novo saldo'))!
    expect((button.element as HTMLButtonElement).disabled).toBe(true)
    expect(wrapper.text()).toContain('Conta inativa')
    wrapper.unmount()
  })

  it('conta sem snapshot mostra o convite e o estado vazio do histórico', async () => {
    mock.onGet('/accounts/a1').reply(200, makeAccount({ latestSnapshot: null, projectedBalance: null }))
    mock.onGet('/accounts/a1/balance-snapshots').reply(200, [])
    const wrapper = await mountAt()
    expect(wrapper.find('[data-testid="no-snapshot"]').text()).toContain('Informe um saldo para ver a projeção')
    expect(wrapper.text()).toContain('Nenhum saldo informado')
    wrapper.unmount()
  })

  it('404 mostra erro com "Tentar novamente"', async () => {
    mock.onGet('/accounts/a1').reply(404, { title: 'Recurso não encontrado.', code: 'not_found', status: 404 })
    const wrapper = await mountAt()
    expect(wrapper.find('[role="alert"]').text()).toContain('Recurso não encontrado.')
    wrapper.unmount()
  })
})
