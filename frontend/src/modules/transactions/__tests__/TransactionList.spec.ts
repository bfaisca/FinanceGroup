import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import DataTable from 'primevue/datatable'
import { createPinia } from 'pinia'
import { makeTransaction } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionList from '../components/TransactionList.vue'

const baseProps = { totalCount: 2, page: 1, pageSize: 20, sort: '-transactionDate' }

function mountList(props: Record<string, unknown>) {
  return mountWithPlugins(TransactionList, { props: { ...baseProps, ...props } as never, pinia: createPinia() }).wrapper
}

describe('TransactionList (mobile: cartões)', () => {
  it('mostra todos os dados essenciais em cada cartão', () => {
    const wrapper = mountList({
      items: [
        makeTransaction({
          id: 'a',
          description: 'Aluguel',
          status: 'Overdue',
          accountName: 'Nubank',
          dueDate: '2026-11-10',
        }),
        makeTransaction({
          id: 'b',
          description: 'Salário',
          type: 'Income',
          amount: 5000,
          paidAmount: 2000,
          remainingAmount: 3000,
          status: 'Pending',
          categoryName: 'Salário',
        }),
      ],
    })

    expect(wrapper.find('table').exists()).toBe(false)
    const cards = wrapper.findAll('.tx-card')
    expect(cards).toHaveLength(2)
    expect(cards[0]!.text()).toContain('Aluguel')
    expect(cards[0]!.text()).toContain('Atrasado')
    expect(cards[0]!.text()).toContain('Vencimento: 10/11/2026')
    expect(cards[0]!.text()).toContain('Conta: Nubank')
    expect(cards[1]!.text()).toMatch(/Restante:\s*R\$\s*3\.000,00/)
    expect(cards[1]!.text()).toMatch(/\+\s*R\$\s*5\.000,00/)
    wrapper.unmount()
  })

  it('emite `open` ao ativar um cartão (é um botão focável)', async () => {
    const tx = makeTransaction()
    const wrapper = mountList({ items: [tx] })
    const card = wrapper.find('button.tx-card')
    expect(card.attributes('aria-label')).toContain('Aluguel')
    await card.trigger('click')
    expect(wrapper.emitted('open')?.[0]).toEqual([tx])
    wrapper.unmount()
  })

  it('estado vazio sem filtros convida a registrar; com filtros sugere limpar', async () => {
    const wrapper = mountList({ items: [], totalCount: 0 })
    expect(wrapper.text()).toContain('Nenhuma movimentação ainda')
    await wrapper.setProps({ filtered: true })
    expect(wrapper.text()).toContain('Nenhuma movimentação encontrada')
    wrapper.unmount()
  })

  it('mostra carregando na primeira carga', () => {
    const wrapper = mountList({ items: [], loading: true, totalCount: 0 })
    expect(wrapper.text()).toContain('Carregando movimentações')
    wrapper.unmount()
  })

  it('mostra erro com "Tentar novamente"', async () => {
    const wrapper = mountList({ items: [], totalCount: 0, error: 'Falha de rede.' })
    expect(wrapper.find('[role="alert"]').text()).toContain('Falha de rede.')
    await wrapper
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
    wrapper.unmount()
  })
})

describe('TransactionList (tablet/desktop: tabela)', () => {
  const original = window.matchMedia
  afterEach(() => {
    window.matchMedia = original
    vi.restoreAllMocks()
  })

  function desktop() {
    window.matchMedia = ((query: string) => ({
      matches: true,
      media: query,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
      addListener: () => undefined,
      removeListener: () => undefined,
    })) as unknown as typeof window.matchMedia
  }

  it('renderiza tabela com cabeçalhos e emite ordenação e abertura', async () => {
    desktop()
    const tx = makeTransaction({ description: 'Internet' })
    const wrapper = mountList({ items: [tx] })
    await flushPromises()

    const headers = wrapper.findAll('th').map((h) => h.text())
    expect(headers).toEqual(expect.arrayContaining(['Data', 'Descrição', 'Conta', 'Vencimento', 'Situação', 'Valor']))
    expect(wrapper.find('.tx-card').exists()).toBe(false)

    wrapper.findComponent(DataTable).vm.$emit('sort', { sortField: 'amount', sortOrder: -1 })
    wrapper.findComponent(DataTable).vm.$emit('sort', { sortField: 'dueDate', sortOrder: 1 })
    expect(wrapper.emitted('update:sort')).toEqual([['-amount'], ['dueDate']])

    await wrapper.find('.tx-list__link').trigger('click')
    wrapper.findComponent(DataTable).vm.$emit('row-click', { data: tx })
    expect(wrapper.emitted('open')?.[0]).toEqual([tx])
    wrapper.unmount()
  })

  it('o botão da descrição abre o detalhe com um clique real (o DataTable ignora cliques em botões da linha)', async () => {
    desktop()
    const tx = makeTransaction({ description: 'Internet' })
    const wrapper = mountList({ items: [tx] })
    await flushPromises()

    await wrapper.find('.tx-list__link').trigger('click')

    expect(wrapper.emitted('open')).toEqual([[tx]])
    wrapper.unmount()
  })

  it('oculta a coluna de conta quando solicitado', async () => {
    desktop()
    const wrapper = mountList({ items: [makeTransaction()], hideAccount: true })
    await flushPromises()
    expect(wrapper.findAll('th').map((h) => h.text())).not.toContain('Conta')
    wrapper.unmount()
  })
})
