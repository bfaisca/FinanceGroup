import type { CategoryDto, CategoryType } from '@/types/api'

export interface CategoryNode {
  category: CategoryDto
  children: CategoryDto[]
}

export interface CategoryOption {
  value: string
  label: string
  /** 0 = raiz, 1 = subcategoria. */
  depth: number
  color: string | null
  icon: string | null
  isActive: boolean
}

const collator = new Intl.Collator('pt-BR', { sensitivity: 'base' })

/** Padrão do sistema primeiro, depois personalizadas; em cada grupo, ordem alfabética (pt-BR). */
function compare(a: CategoryDto, b: CategoryDto): number {
  if (a.isDefault !== b.isDefault) return a.isDefault ? -1 : 1
  return collator.compare(a.name, b.name)
}

/**
 * Monta a árvore (raízes → subcategorias) de um tipo a partir da lista plana do contrato.
 * Subcategoria cujo pai não está na lista é exibida como raiz, para não esconder dados.
 */
export function buildCategoryTree(items: readonly CategoryDto[], type: CategoryType): CategoryNode[] {
  const ofType = items.filter((c) => c.type === type)
  const ids = new Set(ofType.map((c) => c.id))
  const roots = ofType.filter((c) => c.parentCategoryId === null || !ids.has(c.parentCategoryId)).sort(compare)

  return roots.map((root) => ({
    category: root,
    children: ofType.filter((c) => c.parentCategoryId === root.id).sort(compare),
  }))
}

/** Opções planas para seletores: raiz seguida de suas subcategorias (rótulo "Pai › Filha"). Só ativas por padrão. */
export function buildCategoryOptions(
  items: readonly CategoryDto[],
  type: CategoryType,
  options: { includeInactive?: boolean; alwaysInclude?: string | null } = {},
): CategoryOption[] {
  const visible = (c: CategoryDto) => options.includeInactive || c.isActive || c.id === options.alwaysInclude
  const result: CategoryOption[] = []
  for (const node of buildCategoryTree(items, type)) {
    const parent = node.category
    const children = node.children.filter(visible)
    if (!visible(parent) && children.length === 0) continue
    if (visible(parent)) {
      result.push({
        value: parent.id,
        label: parent.name,
        depth: 0,
        color: parent.color,
        icon: parent.icon,
        isActive: parent.isActive,
      })
    }
    for (const child of children) {
      result.push({
        value: child.id,
        label: `${parent.name} › ${child.name}`,
        depth: 1,
        color: child.color ?? parent.color,
        icon: child.icon ?? parent.icon,
        isActive: child.isActive,
      })
    }
  }
  return result
}
