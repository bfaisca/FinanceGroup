import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia } from 'pinia'
import Select from 'primevue/select'
import { useCategoriesStore } from '@/stores/categories'
import { makeCategory } from '@/test/fixtures'
import { mountWithPlugins } from '@/test/mount'
import TransactionFiltersBar from '../components/TransactionFiltersBar.vue'
import { defaultFilters, type TransactionFilters } from '../filters'

function setup(filters: TransactionFilters = defaultFilters(), props: Record<string, unknown> = {}) {
  const pinia = createPinia()
  const { wrapper } = mountWithPlugins(TransactionFiltersBar, { props: { modelValue: filters, ...props }, pinia })
  return wrapper
}

function lastEmitted(wrapper: ReturnType<typeof setup>): TransactionFilters {
  const events = wrapper.emitted('update:modelValue') ?? []
  return events[events.length - 1]?.[0] as TransactionFilters
}

describe('TransactionFiltersBar', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('busca por descrição com debounce e volta para a página 1', async () => {
    const wrapper = setup(defaultFilters({ page: 4 }))
    const input = wrapper.find('input[name="search"]')

    await input.setValue('merc')
    await input.setValue('mercado')
    vi.advanceTimersByTime(299)
    expect(wrapper.emitted('update:modelValue')).toBeUndefined()

    vi.advanceTimersByTime(1)
    expect(wrapper.emitted('update:modelValue')).toHaveLength(1)
    expect(lastEmitted(wrapper)).toMatchObject({ search: 'mercado', page: 1 })
    wrapper.unmount()
  })

  it('Enter aplica a busca imediatamente', async () => {
    const wrapper = setup()
    const input = wrapper.find('input[name="search"]')
    await input.setValue('luz')
    await input.trigger('keydown', { key: 'Enter' })
    expect(lastEmitted(wrapper)).toMatchObject({ search: 'luz' })
    wrapper.unmount()
  })

  it('reflete no campo a busca vinda da URL e limpa tudo em "Limpar filtros"', async () => {
    const wrapper = setup(defaultFilters({ search: 'aluguel', type: 'Expense', status: 'Overdue' }))
    expect((wrapper.find('input[name="search"]').element as HTMLInputElement).value).toBe('aluguel')
    expect(wrapper.text()).toContain('Filtros (3)')

    const clear = wrapper.findAll('button').find((b) => b.text().includes('Limpar filtros'))!
    await clear.trigger('click')

    expect(lastEmitted(wrapper)).toEqual({ page: 1, pageSize: 20, sort: '-transactionDate', accountId: undefined })
    expect((wrapper.find('input[name="search"]').element as HTMLInputElement).value).toBe('')
    wrapper.unmount()
  })

  it('trocar o tipo remove a categoria selecionada (pode ser do outro tipo)', async () => {
    const pinia = createPinia()
    const { wrapper } = mountWithPlugins(TransactionFiltersBar, {
      props: { modelValue: defaultFilters({ categoryId: 'c1' }) },
      pinia,
    })
    const selects = wrapper.findAllComponents(Select)
    // Ordem: tipo, situação, categoria, conta, ordenação.
    selects[0]!.vm.$emit('update:modelValue', 'Income')
    expect(lastEmitted(wrapper)).toMatchObject({ type: 'Income', categoryId: undefined, page: 1 })
    wrapper.unmount()
  })

  it('filtra por situação incluindo Atrasado', async () => {
    const wrapper = setup()
    const selects = wrapper.findAllComponents(Select)
    const status = selects[1]!
    const labels = (status.props('options') as { label: string }[]).map((o) => o.label)
    expect(labels).toEqual(['Todas as situações', 'Previsto', 'Pendente', 'Pago', 'Recebido', 'Atrasado', 'Cancelado'])
    status.vm.$emit('update:modelValue', 'Overdue')
    expect(lastEmitted(wrapper)).toMatchObject({ status: 'Overdue' })
    wrapper.unmount()
  })

  it('agrupa categorias em Despesas e Receitas, com subcategorias', () => {
    const pinia = createPinia()
    const store = useCategoriesStore(pinia)
    store.items = [
      makeCategory({ id: 'e1', name: 'Moradia' }),
      makeCategory({ id: 'e2', name: 'Aluguel', parentCategoryId: 'e1', isDefault: false }),
      makeCategory({ id: 'i1', name: 'Salário', type: 'Income' }),
    ]
    const { wrapper } = mountWithPlugins(TransactionFiltersBar, { props: { modelValue: defaultFilters() }, pinia })
    const category = wrapper.findAllComponents(Select)[2]!
    const groups = category.props('options') as { label: string; items: { label: string }[] }[]
    expect(groups.map((g) => g.label)).toEqual(['Despesas', 'Receitas'])
    expect(groups[0]?.items.map((i) => i.label)).toEqual(['Moradia', 'Moradia › Aluguel'])
    wrapper.unmount()
  })

  it('oculta o filtro de conta quando a tela já está restrita a uma conta', () => {
    const wrapper = setup(defaultFilters(), { hideAccount: true })
    expect(wrapper.find('#filter-account').exists()).toBe(false)
    wrapper.unmount()
  })
})
