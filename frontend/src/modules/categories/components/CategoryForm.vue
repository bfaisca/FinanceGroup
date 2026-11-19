<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import ChoiceGroup from '@/components/ChoiceGroup.vue'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import type { CategoryDto, CategoryType, CreateCategoryRequest, UpdateCategoryRequest } from '@/types/api'
import { CATEGORY_TYPE_LABELS, enumOptions } from '@/utils/labels'
import { CATEGORY_COLORS, CATEGORY_ICONS } from '../options'
import {
  buildCreateCategoryRequest,
  buildUpdateCategoryRequest,
  CATEGORY_FORM_FIELDS,
  validateCategoryForm,
  type CategoryFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Categoria em edição; `null` = criação. */
    category?: CategoryDto | null
    presetType?: CategoryType
    /** Cria como subcategoria desta raiz. */
    presetParentId?: string | null
    /** Categorias raiz ativas (qualquer tipo) disponíveis como pai. */
    roots: CategoryDto[]
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  {
    category: null,
    presetType: 'Expense',
    presetParentId: null,
    loading: false,
    errorMessage: null,
    fieldErrors: () => ({}),
  },
)

const emit = defineEmits<{ submit: [request: CreateCategoryRequest | UpdateCategoryRequest]; cancel: [] }>()

const isEdit = props.category !== null
const { errors, clear, setLocal, setServer } = useFormErrors(CATEGORY_FORM_FIELDS)

const parentOnCreate = props.presetParentId ? props.roots.find((r) => r.id === props.presetParentId) : undefined

const form = reactive<CategoryFormValues>({
  name: props.category?.name ?? '',
  type: props.category?.type ?? parentOnCreate?.type ?? props.presetType,
  parentCategoryId: props.category?.parentCategoryId ?? parentOnCreate?.id ?? null,
  icon: props.category?.icon ?? null,
  color: props.category?.color ?? null,
})

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const typeOptions = enumOptions(CATEGORY_TYPE_LABELS)
const parentOptions = computed(() =>
  props.roots.filter((r) => r.type === form.type).map((r) => ({ value: r.id as string | null, label: r.name })),
)
const parent = computed(() => props.roots.find((r) => r.id === form.parentCategoryId))
const isSubcategory = computed(() => form.parentCategoryId !== null)

function onTypeChange(value: CategoryType | null): void {
  if (!value || value === form.type) return
  form.type = value
  // O pai precisa ter o mesmo tipo.
  if (parent.value && parent.value.type !== value) form.parentCategoryId = null
}

function onParentChange(value: string | null): void {
  form.parentCategoryId = value
  const chosen = props.roots.find((r) => r.id === value)
  // Subcategoria herda o tipo do pai.
  if (chosen) form.type = chosen.type
  clear('parentCategoryId')
}

function onSubmit(): void {
  if (!setLocal(validateCategoryForm(form))) return
  emit('submit', isEdit ? buildUpdateCategoryRequest(form) : buildCreateCategoryRequest(form))
}
</script>

<template>
  <form
    class="form-stack"
    novalidate
    :aria-label="isEdit ? 'Editar categoria' : 'Nova categoria'"
    @submit.prevent="onSubmit"
  >
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField id="cat-name" v-slot="{ id, describedBy, invalid }" label="Nome" :error="errors.name" required>
      <InputText
        :id="id"
        v-model="form.name"
        name="name"
        maxlength="60"
        autocomplete="off"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        autofocus
        fluid
        @input="clear('name')"
      />
    </FormField>

    <template v-if="!isEdit">
      <fieldset class="category-form__type">
        <legend>Tipo</legend>
        <SelectButton
          :model-value="form.type"
          :options="typeOptions"
          option-label="label"
          option-value="value"
          :allow-empty="false"
          :disabled="isSubcategory"
          aria-label="Tipo da categoria"
          @update:model-value="onTypeChange"
        />
        <small v-if="isSubcategory" class="category-form__hint"
          >A subcategoria usa o mesmo tipo da categoria pai.</small
        >
      </fieldset>

      <FormField
        id="cat-parent"
        v-slot="{ id, describedBy, invalid }"
        label="Categoria pai"
        :error="errors.parentCategoryId"
        hint="Opcional. Deixe vazio para criar uma categoria principal."
      >
        <Select
          :model-value="form.parentCategoryId"
          :input-id="id"
          :options="parentOptions"
          option-label="label"
          option-value="value"
          placeholder="Nenhuma (categoria principal)"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="onParentChange"
        />
      </FormField>
    </template>

    <ChoiceGroup
      v-model="form.icon"
      legend="Ícone"
      name="category-icon"
      kind="icon"
      :options="CATEGORY_ICONS"
      none-label="Sem ícone"
    />
    <ChoiceGroup
      v-model="form.color"
      legend="Cor"
      name="category-color"
      kind="color"
      :options="CATEGORY_COLORS"
      none-label="Sem cor"
    />
    <small v-if="errors.color" class="category-form__error" role="alert">{{ errors.color }}</small>
    <small v-if="errors.icon" class="category-form__error" role="alert">{{ errors.icon }}</small>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button
        type="submit"
        :label="isEdit ? 'Salvar alterações' : 'Criar categoria'"
        icon="pi pi-check"
        :loading="loading"
      />
    </div>
  </form>
</template>

<style scoped>
.category-form__type {
  margin: 0;
  padding: 0;
  border: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.category-form__type legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 500;
}

.category-form__hint {
  color: var(--gf-text-muted);
}

.category-form__error {
  color: var(--gf-danger);
}
</style>
