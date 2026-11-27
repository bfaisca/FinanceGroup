import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { createPinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { http } from '@/services/http'
import { makeAccount, makeCreditCard } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import CreditCardsPage from '../pages/CreditCardsPage.vue'

async function mountPage() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/credit-cards', name: 'credit-cards', component: CreditCardsPage },
      { path: '/transactions', name: 'transactions', component: { template: '<div />' } },
    ],
  })
  await router.push('/credit-cards')
  await router.isReady()
  const { wrapper } = mountWithPlugins(CreditCardsPage, { pinia: createPinia(), router })
  await flushPromises()
  return { wrapper, router }
}

function button(root: Element | Document, label: string | RegExp): HTMLButtonElement | undefined {
  return Array.from(root.querySelectorAll('button')).find((b) =>
    typeof label === 'string' ? b.textContent?.trim() === label : label.test(b.textContent ?? ''),
  )
}

describe('CreditCardsPage', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/accounts').reply(200, [makeAccount()])
  })
  afterEach(() => mock.restore())

  it('mostra carregando e depois os cartões com limite, ciclo, fatura aberta e conta associada', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    const { wrapper } = await mountPage()

    const text = wrapper.text()
    expect(text).toContain('Nubank Roxinho')
    expect(text).toContain('Nu Pagamentos')
    expect(wrapper.find('[data-testid="card-cycle"]').text()).toBe('Fecha dia 5 · vence dia 12')
    expect(text).toMatch(/Utilizado\s*R\$\s*1\.250,00\s*de\s*R\$\s*5\.000,00\s*\(25,0%\)/)
    expect(wrapper.find('[data-testid="open-invoice-amount"]').text()).toMatch(/R\$\s*480,90/)
    expect(text).toContain('Fatura aberta (resumo)')
    expect(text).toContain('Fecha em 05/12/2026')
    expect(text).toContain('vence em 12/12/2026')
    expect(text).toContain('não faz a gestão completa de faturas')
    expect(wrapper.find('[data-testid="card-account"]').text()).toContain('Nubank')
    expect(mock.history.get.find((r) => r.url === '/credit-cards')?.params).toEqual({ includeInactive: true })
    wrapper.unmount()
  })

  it('limite excedido aparece com alerta textual', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard({ usedLimit: 6000, availableLimit: -1000 })])
    const { wrapper } = await mountPage()
    expect(wrapper.find('[data-testid="limit-exceeded"]').text()).toContain('Limite excedido')
    wrapper.unmount()
  })

  it('estado vazio guia a cadastrar o primeiro cartão e abre o formulário', async () => {
    mock.onGet('/credit-cards').reply(200, [])
    const { wrapper } = await mountPage()

    expect(wrapper.text()).toContain('Nenhum cartão cadastrado')
    button(wrapper.element, 'Cadastrar primeiro cartão')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('Novo cartão')
    wrapper.unmount()
  })

  it('mostra erro com "Tentar novamente" e se recupera', async () => {
    mock.onGet('/credit-cards').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountPage()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.text()).toContain('Nubank Roxinho')
    wrapper.unmount()
  })

  it('inativos ficam ocultos até marcar "Mostrar cartões inativos"', async () => {
    mock
      .onGet('/credit-cards')
      .reply(200, [makeCreditCard(), makeCreditCard({ id: 'cc2', name: 'Velho', isActive: false })])
    const { wrapper } = await mountPage()
    expect(wrapper.text()).not.toContain('Velho')

    await wrapper.find('#show-inactive-cards').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('Velho')
    expect(wrapper.text()).toContain('Inativo')
    expect(button(wrapper.element, 'Reativar')).toBeDefined()
    wrapper.unmount()
  })

  it('desativar pede confirmação e chama POST /deactivate', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    mock.onPost('/credit-cards/cc1/deactivate').reply(200, makeCreditCard({ isActive: false }))
    const { wrapper } = await mountPage()

    button(wrapper.element, 'Desativar')!.click()
    await flushPromises()
    expect(document.body.textContent).toContain('deixa de aceitar novas compras')
    expect(mock.history.post).toHaveLength(0)

    ;(document.body.querySelector('.confirm-dialog button[type="submit"]') as HTMLButtonElement).click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/credit-cards/cc1/deactivate'])
    wrapper.unmount()
  })

  it('reativar não pede confirmação', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard({ isActive: false })])
    mock.onPost('/credit-cards/cc1/activate').reply(200, makeCreditCard())
    const { wrapper } = await mountPage()
    await wrapper.find('#show-inactive-cards').trigger('click')
    button(wrapper.element, 'Reativar')!.click()
    await flushPromises()
    expect(mock.history.post.map((r) => r.url)).toEqual(['/credit-cards/cc1/activate'])
    wrapper.unmount()
  })

  it('"Ver compras" navega com um clique real para /transactions?creditCardId=…', async () => {
    mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
    const { wrapper, router } = await mountPage()

    button(wrapper.element, 'Ver compras')!.click()
    await flushPromises()
    expect(router.currentRoute.value.name).toBe('transactions')
    expect(router.currentRoute.value.query).toEqual({ creditCardId: 'cc1' })
    wrapper.unmount()
  })

  describe('Pagar fatura (D-057)', () => {
    const invoices = [
      {
        dueDate: '2026-11-12',
        totalAmount: 200,
        paidAmount: 0,
        remainingAmount: 200,
        purchaseCount: 2,
        isOverdue: true,
      },
      {
        dueDate: '2026-12-12',
        totalAmount: 480.9,
        paidAmount: 0,
        remainingAmount: 480.9,
        purchaseCount: 3,
        isOverdue: false,
      },
    ]

    it('lista as faturas em aberto, pré-seleciona a aberta e paga de uma vez recarregando os cartões', async () => {
      mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
      mock.onGet('/credit-cards/cc1/invoices').reply(200, invoices)
      mock.onPost('/credit-cards/cc1/invoices/pay').reply(200, {
        dueDate: '2026-12-12',
        paymentDate: '2026-11-27',
        paidAmount: 480.9,
        purchasesPaid: 3,
      })
      const { wrapper } = await mountPage()

      button(wrapper.element, 'Pagar fatura')!.click()
      await flushPromises()

      expect(document.body.textContent).toContain('Vence em 12/11/2026')
      expect(document.body.textContent).toContain('atrasada')
      expect(document.body.textContent).toContain('3 compras')
      const selected = document.body.querySelector<HTMLInputElement>('input[name="invoice"]:checked')
      expect(selected?.value).toBe('2026-12-12')

      const loadsBefore = mock.history.get.filter((r) => r.url === '/credit-cards').length
      const submit = document.body.querySelector<HTMLButtonElement>('.p-dialog button[type="submit"]')!
      submit.click()
      await flushPromises()

      const post = mock.history.post.find((r) => r.url === '/credit-cards/cc1/invoices/pay')!
      expect(JSON.parse(post.data)).toMatchObject({ dueDate: '2026-12-12' })
      expect(mock.history.get.filter((r) => r.url === '/credit-cards').length).toBe(loadsBefore + 1)
      wrapper.unmount()
    })

    it('sem faturas em aberto avisa e não permite confirmar', async () => {
      mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
      mock.onGet('/credit-cards/cc1/invoices').reply(200, [])
      const { wrapper } = await mountPage()

      button(wrapper.element, 'Pagar fatura')!.click()
      await flushPromises()

      expect(document.body.querySelector('[data-testid="no-invoices"]')?.textContent).toContain(
        'Não há faturas em aberto',
      )
      expect(document.body.querySelector<HTMLButtonElement>('.p-dialog button[type="submit"]')!.disabled).toBe(true)
      wrapper.unmount()
    })

    it('mostra o erro devolvido pela API ao pagar', async () => {
      mock.onGet('/credit-cards').reply(200, [makeCreditCard()])
      mock.onGet('/credit-cards/cc1/invoices').reply(200, invoices)
      mock.onPost('/credit-cards/cc1/invoices/pay').reply(409, { title: 'Conflito com um registro existente.' })
      const { wrapper } = await mountPage()

      button(wrapper.element, 'Pagar fatura')!.click()
      await flushPromises()
      document.body.querySelector<HTMLButtonElement>('.p-dialog button[type="submit"]')!.click()
      await flushPromises()

      expect(document.body.querySelector('.p-dialog [role="alert"]')?.textContent).toContain('Conflito')
      wrapper.unmount()
    })
  })
})
