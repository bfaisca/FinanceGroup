<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useCategoriesStore } from '@/stores/categories'
import type { CategoryDto, CategoryType, CreateCategoryRequest, UpdateCategoryRequest } from '@/types/api'
import { toAppError } from '@/utils/problem'
import { CATEGORY_FORM_FIELDS } from '../validation'
import CategoryForm from './CategoryForm.vue'

const props = withDefaults(
  defineProps<{
    category?: CategoryDto | null
    presetType?: CategoryType
    presetParentId?: string | null
  }>(),
  { category: null, presetType: 'Expense', presetParentId: null },
)

const visible = defineModel<boolean>('visible', { required: true })

const toast = useToast()
const categories = useCategoriesStore()
useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
const formKey = ref(0)

const roots = computed(() => categories.items.filter((c) => c.parentCategoryId === null && c.isActive))

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
  }
})

const header = computed(() => {
  if (props.category) return 'Editar categoria'
  return props.presetParentId ? 'Nova subcategoria' : 'Nova categoria'
})

async function onSubmit(request: CreateCategoryRequest | UpdateCategoryRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    if (props.category) await categories.update(props.category.id, request as UpdateCategoryRequest)
    else await categories.create(request as CreateCategoryRequest)
    visible.value = false
    toast.add({
      severity: 'success',
      summary: props.category ? 'Categoria atualizada' : 'Categoria criada',
      life: 3500,
    })
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (CATEGORY_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    errorMessage.value = mapped ? null : appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="header"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(32rem, calc(100vw - 1.5rem))' }"
  >
    <CategoryForm
      :key="formKey"
      :category="category"
      :preset-type="presetType"
      :preset-parent-id="presetParentId"
      :roots="roots"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
