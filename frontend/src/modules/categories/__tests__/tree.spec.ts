import { describe, expect, it } from 'vitest'
import { makeCategory } from '@/test/fixtures'
import { buildCategoryOptions, buildCategoryTree } from '../tree'

const items = [
  makeCategory({ id: 'outros', name: 'Outros' }),
  makeCategory({ id: 'moradia', name: 'Moradia' }),
  makeCategory({ id: 'pets', name: 'Pets', isDefault: false }),
  makeCategory({ id: 'aluguel', name: 'Aluguel', parentCategoryId: 'moradia', isDefault: false }),
  makeCategory({ id: 'luz', name: 'Luz', parentCategoryId: 'moradia', isDefault: false, isActive: false }),
  makeCategory({ id: 'salario', name: 'Salário', type: 'Income' }),
]

describe('buildCategoryTree', () => {
  it('separa por tipo e aninha subcategorias sob o pai', () => {
    const expense = buildCategoryTree(items, 'Expense')
    expect(expense.map((n) => n.category.id)).toEqual(['moradia', 'outros', 'pets'])
    expect(expense[0]?.children.map((c) => c.id)).toEqual(['aluguel', 'luz'])
    expect(buildCategoryTree(items, 'Income').map((n) => n.category.id)).toEqual(['salario'])
  })

  it('coloca categorias padrão antes das personalizadas', () => {
    const ids = buildCategoryTree(items, 'Expense').map((n) => n.category.id)
    expect(ids.indexOf('outros')).toBeLessThan(ids.indexOf('pets'))
  })

  it('subcategoria sem pai na lista aparece como raiz (não some)', () => {
    const orphan = makeCategory({ id: 'x', name: 'Órfã', parentCategoryId: 'inexistente', isDefault: false })
    expect(buildCategoryTree([orphan], 'Expense').map((n) => n.category.id)).toEqual(['x'])
  })
})

describe('buildCategoryOptions', () => {
  it('lista pai seguido das filhas, somente ativas, com rótulo "Pai › Filha"', () => {
    const options = buildCategoryOptions(items, 'Expense')
    expect(options.map((o) => o.label)).toEqual(['Moradia', 'Moradia › Aluguel', 'Outros', 'Pets'])
    expect(options.map((o) => o.depth)).toEqual([0, 1, 0, 0])
  })

  it('inclui inativas quando pedido e sempre inclui a categoria atual', () => {
    expect(buildCategoryOptions(items, 'Expense', { includeInactive: true }).map((o) => o.value)).toContain('luz')
    expect(buildCategoryOptions(items, 'Expense', { alwaysInclude: 'luz' }).map((o) => o.value)).toContain('luz')
  })

  it('subcategoria herda cor e ícone do pai quando não tem os seus', () => {
    const [, child] = buildCategoryOptions(
      [
        makeCategory({ id: 'p', color: '#111111', icon: 'pi-home' }),
        makeCategory({ id: 'f', name: 'Filha', parentCategoryId: 'p', color: null, icon: null, isDefault: false }),
      ],
      'Expense',
    )
    expect(child).toMatchObject({ color: '#111111', icon: 'pi-home' })
  })
})
