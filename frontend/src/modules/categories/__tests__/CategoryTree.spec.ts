import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import PrimeVue from 'primevue/config'
import { makeCategory } from '@/test/fixtures'
import CategoryTree from '../components/CategoryTree.vue'

const items = [
  makeCategory({ id: 'moradia', name: 'Moradia' }),
  makeCategory({ id: 'pets', name: 'Pets', isDefault: false }),
  makeCategory({ id: 'aluguel', name: 'Aluguel', parentCategoryId: 'moradia', isDefault: false }),
  makeCategory({ id: 'antiga', name: 'Antiga', isDefault: false, isActive: false }),
  makeCategory({ id: 'salario', name: 'Salário', type: 'Income' }),
]

function mountTree(type: 'Expense' | 'Income' = 'Expense') {
  return mount(CategoryTree, { props: { items, type }, global: { plugins: [[PrimeVue, { theme: 'none' }]] } })
}

describe('CategoryTree', () => {
  it('mostra somente o tipo selecionado, com subcategorias aninhadas', () => {
    const wrapper = mountTree()
    expect(wrapper.text()).toContain('Moradia')
    expect(wrapper.text()).toContain('Aluguel')
    expect(wrapper.text()).not.toContain('Salário')
    expect(wrapper.find('ul[aria-label="Subcategorias de Moradia"]').text()).toContain('Aluguel')
  })

  it('categoria padrão: badge "Padrão", sem editar/desativar, mas aceita subcategoria', () => {
    const wrapper = mountTree()
    const row = wrapper.findAll('.category-tree__node')[0]!
    expect(row.text()).toContain('Padrão')
    expect(row.find('[aria-label="Editar Moradia"]').exists()).toBe(false)
    expect(row.find('[aria-label="Desativar Moradia"]').exists()).toBe(false)
    expect(row.find('[aria-label="Adicionar subcategoria em Moradia"]').exists()).toBe(true)
  })

  it('personalizada: editar e desativar disponíveis; subcategoria não aceita subcategoria', async () => {
    const wrapper = mountTree()
    expect(wrapper.find('[aria-label="Editar Pets"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Adicionar subcategoria em Aluguel"]').exists()).toBe(false)
    await wrapper.find('[aria-label="Editar Aluguel"]').trigger('click')
    await wrapper.find('[aria-label="Desativar Pets"]').trigger('click')
    expect(wrapper.emitted('edit')?.[0]?.[0]).toMatchObject({ id: 'aluguel' })
    expect(wrapper.emitted('toggle')?.[0]?.[0]).toMatchObject({ id: 'pets' })
  })

  it('inativa: badge "Inativa", oferece Reativar e não aceita subcategoria', async () => {
    const wrapper = mountTree()
    expect(wrapper.text()).toContain('Inativa')
    expect(wrapper.find('[aria-label="Reativar Antiga"]').exists()).toBe(true)
    expect(wrapper.find('[aria-label="Adicionar subcategoria em Antiga"]').exists()).toBe(false)
  })

  it('emite add-sub com a categoria pai', async () => {
    const wrapper = mountTree()
    await wrapper.find('[aria-label="Adicionar subcategoria em Moradia"]').trigger('click')
    expect(wrapper.emitted('add-sub')?.[0]?.[0]).toMatchObject({ id: 'moradia' })
  })
})
