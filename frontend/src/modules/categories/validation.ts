import type { CategoryDto, CategoryType, CreateCategoryRequest, UpdateCategoryRequest } from '@/types/api'
import { isValidColor } from './options'

export const CATEGORY_NAME_MAX = 60

export interface CategoryFormValues {
  name: string
  type: CategoryType
  parentCategoryId: string | null
  icon: string | null
  color: string | null
}

export const CATEGORY_FORM_FIELDS = ['name', 'type', 'parentCategoryId', 'icon', 'color'] as const
export type CategoryFormField = (typeof CATEGORY_FORM_FIELDS)[number]

export function validateCategoryForm(values: CategoryFormValues): Partial<Record<CategoryFormField, string>> {
  const errors: Partial<Record<CategoryFormField, string>> = {}
  const name = values.name.trim()
  if (name.length === 0) errors.name = 'Informe o nome da categoria.'
  else if (name.length > CATEGORY_NAME_MAX) errors.name = `O nome deve ter até ${CATEGORY_NAME_MAX} caracteres.`
  if (values.color && !isValidColor(values.color)) errors.color = 'Escolha uma cor da lista.'
  return errors
}

export function buildCreateCategoryRequest(values: CategoryFormValues): CreateCategoryRequest {
  return {
    name: values.name.trim(),
    type: values.type,
    parentCategoryId: values.parentCategoryId,
    icon: values.icon,
    color: values.color,
  }
}

export function buildUpdateCategoryRequest(values: CategoryFormValues): UpdateCategoryRequest {
  return { name: values.name.trim(), icon: values.icon, color: values.color }
}

/** Categorias padrão do sistema são somente leitura (D-022). */
export function canModifyCategory(category: Pick<CategoryDto, 'isDefault'>): boolean {
  return !category.isDefault
}

/** Só categorias raiz aceitam subcategorias (profundidade máxima 1). */
export function canAddSubcategory(category: Pick<CategoryDto, 'parentCategoryId' | 'isActive'>): boolean {
  return category.parentCategoryId === null && category.isActive
}
