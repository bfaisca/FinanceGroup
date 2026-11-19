import { http } from '@/services/http'
import type { CategoryDto, CategoryType, CreateCategoryRequest, UpdateCategoryRequest, Uuid } from '@/types/api'

export interface ListCategoriesOptions {
  type?: CategoryType
  includeInactive?: boolean
}

/** Categorias — docs/API_CONTRACTS.md §7. A listagem é plana; a árvore é montada no cliente. */
export const categoryService = {
  async list(options: ListCategoriesOptions = {}): Promise<CategoryDto[]> {
    const params: Record<string, string | boolean> = {}
    if (options.type) params.type = options.type
    if (options.includeInactive) params.includeInactive = true
    const { data } = await http.get<CategoryDto[]>('/categories', { params })
    return data
  },

  async create(request: CreateCategoryRequest): Promise<CategoryDto> {
    const { data } = await http.post<CategoryDto>('/categories', request)
    return data
  },

  async update(id: Uuid, request: UpdateCategoryRequest): Promise<CategoryDto> {
    const { data } = await http.put<CategoryDto>(`/categories/${id}`, request)
    return data
  },

  async deactivate(id: Uuid): Promise<CategoryDto> {
    const { data } = await http.post<CategoryDto>(`/categories/${id}/deactivate`)
    return data
  },

  async activate(id: Uuid): Promise<CategoryDto> {
    const { data } = await http.post<CategoryDto>(`/categories/${id}/activate`)
    return data
  },
}
