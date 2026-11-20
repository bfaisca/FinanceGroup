<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import IconField from 'primevue/iconfield'
import InputIcon from 'primevue/inputicon'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import { useDebouncedCallback } from '@/composables/useDebouncedCallback'
import { buildCategoryOptions } from '@/modules/categories/tree'
import { SORT_OPTIONS } from '@/modules/transactions/filters'
import { useCategoriesStore } from '@/stores/categories'
import { enumOptions, STATUS_PRESENTATION, TRANSACTION_TYPE_LABELS } from '@/utils/labels'
import { countActiveGroupFilters, type GroupTransactionFilters } from '../filters'
import type { GroupMemberDto } from '../types'

const props = withDefaults(
  defineProps<{
    modelValue: GroupTransactionFilters
    /** Membros (inclusive ex-membros) para os filtros "quem cadastrou" e "quem pagou". */
    members?: GroupMemberDto[]
    debounceMs?: number
  }>(),
  { members: () => [], debounceMs: 300 },
)

const emit = defineEmits<{ 'update:modelValue': [value: GroupTransactionFilters] }>()

const categories = useCategoriesStore()
const searchText = ref(props.modelValue.search ?? '')
const expanded = ref(false)
const activeCount = computed(() => countActiveGroupFilters(props.modelValue))

/** Qualquer mudança de filtro volta para a primeira página. */
function update(patch: Partial<GroupTransactionFilters>): void {
  emit('update:modelValue', { ...props.modelValue, ...patch, page: 1 })
}

const debounced = useDebouncedCallback((value: string) => {
  update({ search: value.trim() || undefined })
}, props.debounceMs)

watch(
  () => props.modelValue.search,
  (value) => {
    if ((value ?? '') !== searchText.value.trim()) {
      debounced.cancel()
      searchText.value = value ?? ''
    }
  },
)

const typeOptions = [{ value: null, label: 'Todos os tipos' }, ...enumOptions(TRANSACTION_TYPE_LABELS)]
const statusOptions = [
  { value: null, label: 'Todas as situações' },
  ...(Object.keys(STATUS_PRESENTATION) as (keyof typeof STATUS_PRESENTATION)[]).map((value) => ({
    value,
    label: STATUS_PRESENTATION[value].label,
  })),
]

interface CategoryGroup {
  label: string
  items: { value: string | null; label: string }[]
}

const categoryGroups = computed<CategoryGroup[]>(() => {
  const groups: CategoryGroup[] = []
  for (const [type, label] of [
    ['Expense', 'Despesas'],
    ['Income', 'Receitas'],
  ] as const) {
    if (props.modelValue.type && props.modelValue.type !== type) continue
    const options = buildCategoryOptions(categories.items, type, { includeInactive: true })
    if (options.length > 0) groups.push({ label, items: options.map((o) => ({ value: o.value, label: o.label })) })
  }
  return groups
})

const memberOptions = computed(() =>
  props.members.map((m) => ({ value: m.userId, label: m.status === 'Active' ? m.name : `${m.name} (ex-membro)` })),
)

function clearAll(): void {
  debounced.cancel()
  searchText.value = ''
  emit('update:modelValue', { page: 1, pageSize: props.modelValue.pageSize, sort: props.modelValue.sort })
}
</script>

<template>
  <section class="gtx-filters" aria-label="Filtros de movimentações do grupo">
    <div class="gtx-filters__top">
      <IconField class="gtx-filters__search">
        <InputIcon class="pi pi-search" aria-hidden="true" />
        <InputText
          v-model="searchText"
          type="search"
          name="search"
          aria-label="Buscar por descrição"
          placeholder="Buscar por descrição"
          autocomplete="off"
          fluid
          @input="debounced.run(searchText)"
          @keydown.enter.prevent="debounced.flush(searchText)"
        />
      </IconField>
      <Button
        class="gtx-filters__toggle"
        type="button"
        :label="activeCount > 0 ? `Filtros (${activeCount})` : 'Filtros'"
        icon="pi pi-filter"
        severity="secondary"
        outlined
        aria-controls="gtx-filters-panel"
        :aria-expanded="expanded"
        @click="expanded = !expanded"
      />
    </div>

    <div id="gtx-filters-panel" class="gtx-filters__panel" :class="{ 'gtx-filters__panel--open': expanded }">
      <div class="gtx-filters__grid">
        <FormField id="gfilter-from" v-slot="{ id, describedBy, invalid }" label="De">
          <DateInput
            :id="id"
            :model-value="modelValue.from ?? null"
            :described-by="describedBy"
            :invalid="invalid"
            :max="modelValue.to ?? null"
            @update:model-value="update({ from: $event ?? undefined })"
          />
        </FormField>
        <FormField id="gfilter-to" v-slot="{ id, describedBy, invalid }" label="Até">
          <DateInput
            :id="id"
            :model-value="modelValue.to ?? null"
            :described-by="describedBy"
            :invalid="invalid"
            :min="modelValue.from ?? null"
            @update:model-value="update({ to: $event ?? undefined })"
          />
        </FormField>
        <FormField id="gfilter-type" v-slot="{ id }" label="Tipo">
          <Select
            :input-id="id"
            :model-value="modelValue.type ?? null"
            :options="typeOptions"
            option-label="label"
            option-value="value"
            placeholder="Todos os tipos"
            fluid
            @update:model-value="update({ type: $event ?? undefined, categoryId: undefined })"
          />
        </FormField>
        <FormField id="gfilter-status" v-slot="{ id }" label="Situação">
          <Select
            :input-id="id"
            :model-value="modelValue.status ?? null"
            :options="statusOptions"
            option-label="label"
            option-value="value"
            placeholder="Todas as situações"
            fluid
            @update:model-value="update({ status: $event ?? undefined })"
          />
        </FormField>
        <FormField
          id="gfilter-category"
          v-slot="{ id }"
          label="Categoria"
          hint="Inclui as subcategorias da categoria escolhida."
        >
          <Select
            :input-id="id"
            :model-value="modelValue.categoryId ?? null"
            :options="categoryGroups"
            option-group-label="label"
            option-group-children="items"
            option-label="label"
            option-value="value"
            placeholder="Todas as categorias"
            show-clear
            filter
            fluid
            @update:model-value="update({ categoryId: $event ?? undefined })"
          />
        </FormField>
        <FormField id="gfilter-created-by" v-slot="{ id }" label="Quem cadastrou">
          <Select
            :input-id="id"
            :model-value="modelValue.createdByUserId ?? null"
            :options="memberOptions"
            option-label="label"
            option-value="value"
            placeholder="Todos os participantes"
            show-clear
            fluid
            @update:model-value="update({ createdByUserId: $event ?? undefined })"
          />
        </FormField>
        <FormField
          id="gfilter-paid-by"
          v-slot="{ id }"
          label="Quem pagou"
          hint="Movimentações com ao menos um pagamento dessa pessoa."
        >
          <Select
            :input-id="id"
            :model-value="modelValue.paidByUserId ?? null"
            :options="memberOptions"
            option-label="label"
            option-value="value"
            placeholder="Todos os participantes"
            show-clear
            fluid
            @update:model-value="update({ paidByUserId: $event ?? undefined })"
          />
        </FormField>
        <FormField id="gfilter-sort" v-slot="{ id }" label="Ordenar por">
          <Select
            :input-id="id"
            :model-value="modelValue.sort"
            :options="SORT_OPTIONS as unknown as { value: string; label: string }[]"
            option-label="label"
            option-value="value"
            placeholder="Ordenar por"
            fluid
            @update:model-value="update({ sort: $event })"
          />
        </FormField>
      </div>
      <div class="gtx-filters__actions">
        <Button
          type="button"
          label="Limpar filtros"
          icon="pi pi-filter-slash"
          severity="secondary"
          text
          :disabled="activeCount === 0"
          @click="clearAll"
        />
      </div>
    </div>
  </section>
</template>

<style scoped>
.gtx-filters {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
  padding: 1rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.gtx-filters__top {
  display: flex;
  gap: 0.5rem;
}

.gtx-filters__search {
  flex: 1;
  min-width: 0;
}

.gtx-filters__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr));
  gap: 0.75rem 1rem;
}

.gtx-filters__actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 0.5rem;
}

@media (max-width: 767px) {
  .gtx-filters__panel:not(.gtx-filters__panel--open) {
    display: none;
  }
}

@media (min-width: 768px) {
  .gtx-filters__toggle {
    display: none;
  }
}
</style>
