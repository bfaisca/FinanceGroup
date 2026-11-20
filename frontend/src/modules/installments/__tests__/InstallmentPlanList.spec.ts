import { afterEach, describe, expect, it } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import DataTable from 'primevue/datatable'
import { createPinia } from 'pinia'
import { makePlan } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import InstallmentPlanList from '../components/InstallmentPlanList.vue'

const baseProps = { totalCount: 2, page: 1, pageSize: 10 }

function mountList(props: Record<string, unknown>) {
  return mountWithPlugins(InstallmentPlanList, { props: { ...baseProps, ...props } as never, pinia: createPinia() })
    .wrapper
}

function desktop(): void {
  window.matchMedia = ((query: string) => ({
    matches: true,
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
  })) as unknown as typeof window.matchMedia
}

describe('InstallmentPlanList (mobile: cartões)', () => {
  it('mostra descrição, progresso, total, restante, próximo vencimento, situação e cartão', () => {
    const wrapper = mountList({
      items: [
        makePlan(),
        makePlan({
          id: 'p2',
          description: 'Sofá',
          status: 'Completed',
          creditCardName: null,
          accountName: 'Nubank',
          nextDueDate: null,
          remainingAmount: 0,
        }),
      ],
    })
    expect(wrapper.find('table').exists()).toBe(false)
    const cards = wrapper.findAll('.plan-card')
    expect(cards).toHaveLength(2)
    expect(cards[0]!.text()).toContain('Notebook')
    expect(cards[0]!.find('[data-testid="plan-progress-text"]').text()).toBe('2/12 pagas')
    expect(cards[0]!.find('[role="progressbar"]').attributes('aria-label')).toContain('2 de 12 parcelas pagas')
    expect(cards[0]!.text()).toMatch(/Total:\s*R\$\s*3\.000,00/)
    expect(cards[0]!.text()).toMatch(/Restante:\s*R\$\s*2\.500,00/)
    expect(cards[0]!.text()).toContain('Próximo vencimento: 12/12/2026')
    expect(cards[0]!.text()).toContain('Em aberto')
    expect(cards[0]!.text()).toContain('Nubank Roxinho')
    expect(cards[1]!.text()).toContain('Concluído')
    expect(cards[1]!.text()).not.toContain('Próximo vencimento')
    wrapper.unmount()
  })

  it('emite `open` ao ativar um cartão', async () => {
    const plan = makePlan()
    const wrapper = mountList({ items: [plan] })
    await wrapper.find('button.plan-card').trigger('click')
    expect(wrapper.emitted('open')).toEqual([[plan]])
    wrapper.unmount()
  })

  it('estados: carregando, erro com retry, vazio (com e sem filtro)', async () => {
    const loading = mountList({ items: [], loading: true, totalCount: 0 })
    expect(loading.text()).toContain('Carregando parcelamentos')
    loading.unmount()

    const failed = mountList({ items: [], totalCount: 0, error: 'Falha de rede.' })
    expect(failed.find('[role="alert"]').text()).toContain('Falha de rede.')
    await failed
      .findAll('button')
      .find((b) => b.text().includes('Tentar novamente'))!
      .trigger('click')
    expect(failed.emitted('retry')).toHaveLength(1)
    failed.unmount()

    const empty = mountList({ items: [], totalCount: 0 })
    expect(empty.text()).toContain('Nenhum parcelamento ainda')
    await empty
      .findAll('button')
      .find((b) => b.text().includes('Novo parcelamento'))!
      .trigger('click')
    expect(empty.emitted('create')).toHaveLength(1)
    await empty.setProps({ filtered: true })
    expect(empty.text()).toContain('Nenhum parcelamento encontrado')
    empty.unmount()
  })
})

describe('InstallmentPlanList (desktop: tabela)', () => {
  const original = window.matchMedia
  afterEach(() => {
    window.matchMedia = original
  })

  it('renderiza cabeçalhos e valores', async () => {
    desktop()
    const wrapper = mountList({ items: [makePlan()] })
    await flushPromises()
    expect(wrapper.findAll('th').map((h) => h.text())).toEqual(
      expect.arrayContaining(['Descrição', 'Progresso', 'Total', 'Restante', 'Próximo vencimento', 'Situação']),
    )
    expect(wrapper.find('tbody').text()).toContain('Notebook')
    expect(wrapper.find('tbody').text()).toContain('12/12/2026')
    wrapper.unmount()
  })

  it('o link da descrição e o botão "Detalhes" abrem com cliques reais (o DataTable ignora botões da linha)', async () => {
    desktop()
    const plan = makePlan()
    const wrapper = mountList({ items: [plan] })
    await flushPromises()

    await wrapper.find('.plan-list__link').trigger('click')
    await wrapper
      .findAll('tbody button')
      .find((b) => b.text().includes('Detalhes'))!
      .trigger('click')
    expect(wrapper.emitted('open')).toEqual([[plan], [plan]])

    wrapper.findComponent(DataTable).vm.$emit('row-click', { data: plan })
    expect(wrapper.emitted('open')).toHaveLength(3)
    wrapper.unmount()
  })
})
