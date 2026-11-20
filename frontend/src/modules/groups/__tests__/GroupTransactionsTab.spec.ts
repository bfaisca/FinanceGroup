import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { flushPromises } from '@vue/test-utils'
import { http } from '@/services/http'
import { makeAccount, makeCategory } from '@/test/fixtures'
import GroupTransactionsTab from '../components/GroupTransactionsTab.vue'
import {
  makeGroupDetail,
  makeGroupMember,
  makeGroupTransaction,
  makeGroupTransactionDetail,
  makeMemberViewDetail,
} from './fixtures'
import { button, buttonLabels, mountAt } from './helpers'

function pageOf(items = [makeGroupTransaction()], totalCount = items.length) {
  return { items, page: 1, pageSize: 20, totalCount, totalPages: Math.max(1, Math.ceil(totalCount / 20)) }
}

const mountTab = (url = '/groups/g1?tab=transactions', group = makeGroupDetail()) =>
  mountAt(GroupTransactionsTab, url, { path: '/groups/:id', name: 'group-detail' }, { props: { group } })

describe('GroupTransactionsTab', () => {
  let mock: MockAdapter
  beforeEach(() => {
    mock = new MockAdapter(http)
    mock.onGet('/categories').reply(200, [makeCategory({ id: 'c-alimentacao', name: 'Alimentação' })])
    mock.onGet('/accounts').reply(200, [makeAccount({ id: 'a1', name: 'Nubank' })])
    mock
      .onGet('/groups/g1/members')
      .reply(200, [
        makeGroupMember({ userId: 'u1', name: 'Maria Silva' }),
        makeGroupMember({ id: 'm9', userId: 'u9', name: 'Carlos Antigo', status: 'Left' }),
      ])
  })
  afterEach(() => mock.restore())

  const listRequests = () => mock.history.get.filter((r) => r.url === '/groups/g1/transactions')

  it('lê os filtros da URL (incluindo quem cadastrou/pagou) e envia os nomes do contrato', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    const { wrapper } = await mountTab(
      '/groups/g1?tab=transactions&from=2026-11-01&to=2026-11-30&type=Expense&status=Overdue&categoryId=c1&createdBy=u1&paidBy=u2&search=luz&page=2&sort=-amount',
    )

    expect(listRequests()[0]?.params).toEqual({
      page: 2,
      pageSize: 20,
      sort: '-amount',
      from: '2026-11-01',
      to: '2026-11-30',
      type: 'Expense',
      status: 'Overdue',
      categoryId: 'c1',
      createdByUserId: 'u1',
      paidByUserId: 'u2',
      search: 'luz',
    })
    expect(wrapper.text()).toContain('Mercado')
    expect(wrapper.text()).toContain('Maria Silva')
    wrapper.unmount()
  })

  it('buscar atualiza a URL (preservando `tab`, sem `page`) e recarrega a lista', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    const { wrapper, router } = await mountTab('/groups/g1?tab=transactions&page=3')

    const input = wrapper.find('input[name="search"]')
    await input.setValue('mercado')
    await input.trigger('keydown', { key: 'Enter' })
    await flushPromises()

    expect(router.currentRoute.value.query).toEqual({ tab: 'transactions', search: 'mercado' })
    expect(listRequests().at(-1)?.params).toMatchObject({ search: 'mercado', page: 1 })
    wrapper.unmount()
  })

  it('os seletores de filtro têm placeholder legível (nunca em branco)', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    const { wrapper } = await mountTab()

    const selects = wrapper.findAll('.p-select')
    expect(selects.length).toBeGreaterThanOrEqual(6)
    for (const select of selects) {
      expect(select.text().trim().length, select.html()).toBeGreaterThan(0)
    }
    expect(wrapper.text()).toContain('Todos os participantes')
    wrapper.unmount()
  })

  it('mostra contagem, estado vazio explicativo e botão de criar para quem pode', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf([], 0))
    const { wrapper } = await mountTab()

    expect(wrapper.text()).toContain('Nenhuma movimentação no grupo ainda')
    expect(buttonLabels(wrapper.element).filter((l) => l === 'Nova movimentação').length).toBeGreaterThan(0)
    wrapper.unmount()
  })

  it('filtro sem resultados mostra mensagem de filtros', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf([], 0))
    const { wrapper } = await mountTab('/groups/g1?tab=transactions&search=xyz')
    expect(wrapper.text()).toContain('Nenhuma movimentação encontrada')
    wrapper.unmount()
  })

  it('erro mostra alerta e permite tentar novamente', async () => {
    mock.onGet('/groups/g1/transactions').replyOnce(500, { title: 'Erro interno.' })
    const { wrapper } = await mountTab()
    expect(wrapper.find('[role="alert"]').text()).toContain('Erro interno.')

    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    await button(wrapper.element, 'Tentar novamente')!.click()
    await flushPromises()
    expect(wrapper.text()).toContain('Mercado')
    wrapper.unmount()
  })

  it('sem `canCreateTransactions` (ex.: grupo arquivado) não oferece criar e avisa somente leitura', async () => {
    mock.onGet('/groups/g1/transactions').reply(200, pageOf())
    const group = makeGroupDetail({
      isActive: false,
      permissions: { ...makeGroupDetail().permissions, canCreateTransactions: false },
    })
    const { wrapper } = await mountTab('/groups/g1?tab=transactions', group)

    expect(buttonLabels(wrapper.element)).not.toContain('Nova movimentação')
    expect(wrapper.find('[data-testid="archived-note"]').text()).toContain('somente para leitura')
    wrapper.unmount()
  })

  describe('formulário (sem dados privados)', () => {
    it('abre o formulário sem campos de conta/cartão e valida a categoria', async () => {
      mock.onGet('/groups/g1/transactions').reply(200, pageOf())
      const { wrapper } = await mountTab()

      await button(wrapper.element, 'Nova movimentação')!.click()
      await flushPromises()

      const body = document.body
      expect(body.textContent).toContain('Contas e cartões pessoais não fazem parte dela')
      expect(body.querySelector('#gtx-account')).toBeNull()
      expect(body.querySelector('#gtx-card')).toBeNull()

      const set = (selector: string, value: string) => {
        const el = body.querySelector<HTMLInputElement>(selector)!
        el.value = value
        el.dispatchEvent(new Event('input'))
      }
      set('#gtx-description', 'Mercado')
      set('#gtx-amount', '200,00')
      body.querySelector('form')!.dispatchEvent(new Event('submit'))
      await flushPromises()
      expect(body.textContent).toContain('Escolha uma categoria.')
      expect(mock.history.post).toHaveLength(0)
      wrapper.unmount()
    })
  })

  describe('detalhe (drawer) e permissões do backend', () => {
    async function openDetail(detail = makeGroupTransactionDetail(), group = makeGroupDetail()) {
      mock.onGet('/groups/g1/transactions').reply(200, pageOf([detail]))
      mock.onGet(`/groups/g1/transactions/${detail.id}`).reply(200, detail)
      return mountTab(`/groups/g1?tab=transactions&open=${detail.id}`, group)
    }

    it('criador vê pagar, editar, cancelar e excluir; mostra quem cadastrou', async () => {
      const { wrapper } = await openDetail()
      const labels = buttonLabels(document.body)
      expect(labels).toContain('Registrar pagamento')
      expect(labels).toContain('Editar')
      expect(labels).toContain('Cancelar movimentação')
      expect(labels).toContain('Excluir')
      expect(document.body.querySelector('[data-testid="detail-created-by"]')?.textContent).toBe('Maria Silva')
      wrapper.unmount()
    })

    it('movimentação de terceiros: só pagar, sem editar/cancelar/excluir', async () => {
      const foreign = makeGroupTransactionDetail({
        createdBy: { userId: 'u2', name: 'João Souza' },
        permissions: { canEdit: false, canCancel: false, canDelete: false, canPay: true },
      })
      const { wrapper } = await openDetail(foreign)
      const labels = buttonLabels(document.body)
      expect(labels).toContain('Registrar pagamento')
      expect(labels).not.toContain('Editar')
      expect(labels).not.toContain('Cancelar movimentação')
      expect(labels).not.toContain('Excluir')
      wrapper.unmount()
    })

    it('com pagamentos explica por que não exclui; remover só aparece no pagamento próprio (`canRemove`)', async () => {
      const detail = makeGroupTransactionDetail({
        paidAmount: 80,
        remainingAmount: 120,
        permissions: { canEdit: true, canCancel: true, canDelete: false, canPay: true },
        payments: [
          {
            id: 'p-own',
            amount: 30,
            paymentDate: '2026-11-10',
            paymentMethod: 'Pix',
            paidBy: { userId: 'u1', name: 'Maria Silva' },
            createdAt: '2026-11-10T10:00:00Z',
            canRemove: true,
          },
          {
            id: 'p-other',
            amount: 50,
            paymentDate: '2026-11-11',
            paymentMethod: null,
            paidBy: { userId: 'u2', name: 'João Souza' },
            createdAt: '2026-11-11T10:00:00Z',
            canRemove: false,
          },
        ],
      })
      const { wrapper } = await openDetail(detail)

      expect(document.body.querySelector('[data-testid="delete-hint"]')?.textContent).toContain(
        'não é possível excluir',
      )
      expect(buttonLabels(document.body)).not.toContain('Excluir')
      const rows = Array.from(document.body.querySelectorAll('.gtx-drawer__payments tbody tr'))
      expect(rows).toHaveLength(2)
      expect(rows[0]?.textContent).toContain('Maria Silva')
      expect(rows[0]?.querySelector('button')).not.toBeNull()
      expect(rows[1]?.textContent).toContain('João Souza')
      expect(rows[1]?.querySelector('button')).toBeNull()
      wrapper.unmount()
    })

    it('remover o próprio pagamento pede confirmação e chama o endpoint', async () => {
      const detail = makeGroupTransactionDetail({
        paidAmount: 30,
        remainingAmount: 170,
        payments: [
          {
            id: 'p-own',
            amount: 30,
            paymentDate: '2026-11-10',
            paymentMethod: null,
            paidBy: { userId: 'u1', name: 'Maria Silva' },
            createdAt: '2026-11-10T10:00:00Z',
            canRemove: true,
          },
        ],
      })
      mock.onDelete('/groups/g1/transactions/gt1/payments/p-own').reply(200, { ...detail, payments: [], paidAmount: 0 })
      const { wrapper } = await openDetail(detail)

      document.body.querySelector<HTMLButtonElement>('.gtx-drawer__payments tbody button')!.click()
      await flushPromises()
      expect(mock.history.delete).toHaveLength(0)
      expect(document.body.textContent).toContain('Remover o seu pagamento')

      document.body.querySelector('.confirm-dialog')!.dispatchEvent(new Event('submit'))
      await flushPromises()
      expect(mock.history.delete).toHaveLength(1)
      wrapper.unmount()
    })

    it('cancelada: criador vê Reabrir e não vê pagar, editar nem cancelar', async () => {
      const { wrapper } = await openDetail(makeGroupTransactionDetail({ status: 'Canceled' }))
      const labels = buttonLabels(document.body.querySelector('.gtx-drawer__actions')!)
      expect(labels).toContain('Reabrir')
      expect(labels).not.toContain('Registrar pagamento')
      expect(labels).not.toContain('Editar')
      expect(labels).not.toContain('Cancelar movimentação')
      wrapper.unmount()
    })

    it('registra o próprio pagamento: valor ≤ restante, data ≤ hoje, conta opcional e privada', async () => {
      const detail = makeGroupTransactionDetail({ amount: 200, remainingAmount: 200 })
      mock
        .onPost('/groups/g1/transactions/gt1/payments')
        .reply(201, { ...detail, paidAmount: 50, remainingAmount: 150 })
      const { wrapper } = await openDetail(detail, makeMemberViewDetail())

      await button(document.body, 'Registrar pagamento')!.click()
      await flushPromises()
      const body = document.body
      expect(body.textContent).toContain('Sua conta (privada)')
      expect(body.textContent).toContain('Sua conta é privada: o grupo não a vê')
      expect(body.textContent).toContain('só afeta o seu saldo individual')
      expect(body.textContent).toContain('Não vincular a uma conta')

      const set = (selector: string, value: string) => {
        const el = body.querySelector<HTMLInputElement>(selector)!
        el.value = value
        el.dispatchEvent(new Event('input'))
      }
      const form = () => body.querySelector<HTMLFormElement>('form[aria-label="Registrar pagamento"]')!

      set('#gpay-amount', '250,00')
      form().dispatchEvent(new Event('submit'))
      await flushPromises()
      expect(body.textContent).toContain('O valor não pode ser maior que o restante')
      expect(mock.history.post).toHaveLength(0)

      set('#gpay-amount', '50,00')
      form().dispatchEvent(new Event('submit'))
      await flushPromises()
      const request = JSON.parse(mock.history.post[0]?.data)
      expect(request).toMatchObject({ amount: 50, paymentDate: '2026-11-18' })
      expect(request.accountId ?? null).toBeNull()
      wrapper.unmount()
    })

    it('histórico mostra o nome de quem agiu', async () => {
      mock.onGet('/groups/g1/transactions/gt1/history').reply(200, [
        {
          id: 'h1',
          action: 'PaymentAdded',
          field: 'payment',
          oldValue: null,
          newValue: '50.00 (2026-11-10)',
          actorName: 'João Souza',
          createdAt: '2026-11-10T10:00:00Z',
        },
      ])
      const { wrapper } = await openDetail()

      const tab = Array.from(document.body.querySelectorAll<HTMLElement>('[role="tab"]')).find(
        (t) => t.textContent?.trim() === 'Histórico',
      )!
      tab.click()
      await flushPromises()

      const history = document.body.querySelector('ol.history')!
      expect(history.textContent).toContain('Pagamento registrado')
      expect(history.textContent).toContain('por João Souza')
      wrapper.unmount()
    })
  })
})
