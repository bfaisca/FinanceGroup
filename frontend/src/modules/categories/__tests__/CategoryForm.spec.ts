import { describe, expect, it } from 'vitest'
import { mountWithPlugins } from '@/test/mount'
import { makeCategory } from '@/test/fixtures'
import CategoryForm from '../components/CategoryForm.vue'
import { buildCreateCategoryRequest, canAddSubcategory, canModifyCategory, validateCategoryForm } from '../validation'

const roots = [
  makeCategory({ id: 'moradia', name: 'Moradia' }),
  makeCategory({ id: 'salario', name: 'Salário', type: 'Income' }),
]

describe('CategoryForm', () => {
  it('exige nome e não emite quando inválido', async () => {
    const { wrapper } = mountWithPlugins(CategoryForm, { props: { roots } })
    await wrapper.find('form').trigger('submit')
    expect(wrapper.find('#cat-name-error').text()).toContain('Informe o nome')
    expect(wrapper.emitted('submit')).toBeUndefined()
    wrapper.unmount()
  })

  it('cria categoria principal com ícone e cor escolhidos', async () => {
    const { wrapper } = mountWithPlugins(CategoryForm, { props: { roots, presetType: 'Income' } })
    await wrapper.find('#cat-name').setValue('  Bônus ')
    await wrapper.find('input[name="category-icon"][value="pi-gift"]').setValue()
    await wrapper.find('input[name="category-color"][value="#16a34a"]').setValue()
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({
      name: 'Bônus',
      type: 'Income',
      parentCategoryId: null,
      icon: 'pi-gift',
      color: '#16a34a',
    })
    wrapper.unmount()
  })

  it('subcategoria herda o tipo do pai e bloqueia a troca de tipo', async () => {
    const { wrapper } = mountWithPlugins(CategoryForm, {
      props: { roots, presetType: 'Income', presetParentId: 'moradia' },
    })
    await wrapper.find('#cat-name').setValue('Aluguel')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toMatchObject({ type: 'Expense', parentCategoryId: 'moradia' })
    expect(wrapper.text()).toContain('mesmo tipo da categoria pai')
    wrapper.unmount()
  })

  it('edição envia só nome, ícone e cor', async () => {
    const category = makeCategory({ id: 'pets', name: 'Pets', isDefault: false, icon: 'pi-heart', color: '#dc2626' })
    const { wrapper } = mountWithPlugins(CategoryForm, { props: { roots, category } })
    expect(wrapper.find('#cat-parent').exists()).toBe(false)
    await wrapper.find('#cat-name').setValue('Animais')
    await wrapper.find('form').trigger('submit')
    expect(wrapper.emitted('submit')?.[0]?.[0]).toEqual({ name: 'Animais', icon: 'pi-heart', color: '#dc2626' })
    wrapper.unmount()
  })

  it('mostra erros do backend (ex.: nome duplicado)', async () => {
    const { wrapper } = mountWithPlugins(CategoryForm, { props: { roots } })
    await wrapper.setProps({ fieldErrors: { name: ['Já existe uma categoria com este nome.'] } })
    expect(wrapper.find('#cat-name-error').text()).toContain('Já existe')
    wrapper.unmount()
  })
})

describe('regras de categoria (UX)', () => {
  it('valida nome e cor', () => {
    const base = { name: 'x', type: 'Expense' as const, parentCategoryId: null, icon: null, color: null }
    expect(validateCategoryForm(base)).toEqual({})
    expect(validateCategoryForm({ ...base, name: ' ' }).name).toBeTruthy()
    expect(validateCategoryForm({ ...base, name: 'x'.repeat(61) }).name).toMatch(/60/)
    expect(validateCategoryForm({ ...base, color: 'vermelho' }).color).toBeTruthy()
    expect(buildCreateCategoryRequest({ ...base, name: ' Pets ' }).name).toBe('Pets')
  })

  it('padrão é somente leitura; só raiz ativa recebe subcategoria', () => {
    expect(canModifyCategory({ isDefault: true })).toBe(false)
    expect(canModifyCategory({ isDefault: false })).toBe(true)
    expect(canAddSubcategory({ parentCategoryId: null, isActive: true })).toBe(true)
    expect(canAddSubcategory({ parentCategoryId: 'p', isActive: true })).toBe(false)
    expect(canAddSubcategory({ parentCategoryId: null, isActive: false })).toBe(false)
  })
})
