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
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { enumOptions, STATUS_PRESENTATION, TRANSACTION_TYPE_LABELS } from '@/utils/labels'
import { countActiveFilters, SORT_OPTIONS, type TransactionFilters } from '../filters'

const props = withDefaults(
  defineProps<{
    modelValue: TransactionFilters
    /** Oculta o filtro de conta (ex.: listagem já restrita a uma conta). */
    hideAccount?: boolean
    debounceMs?: number
  }>(),
  { hideAccount: false, debounceMs: 300 },
)

const emit = defineEmits<{ 'update:modelValue': [value: TransactionFilters] }>()

const categories = useCategoriesStore()
const accounts = useAccountsStore()

const searchText = ref(props.modelValue.search ?? '')
const expanded = ref(false)

const activeCount = computed(() => countActiveFilters(props.modelValue))

/** Qualquer mudança de filtro volta para a primeira página. */
function update(patch: Partial<TransactionFilters>): void {
  emit('update:modelValue', { ...props.modelValue, ...patch, page: 1 })
}

const debounced = useDebouncedCallback((value: string) => {
  update({ search: value.trim() || undefined })
}, props.debounceMs)

function onSearchInput(): void {
  debounced.run(searchText.value)
}

// Mudanças externas (ex.: "Limpar filtros", navegação) refletem no campo de busca.
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
    if (options.length > 0) {
      groups.push({ label, items: options.map((o) => ({ value: o.value, label: o.label })) })
    }
  }
  return groups
})

const accountOptions = computed(() => [
  { value: null, label: 'Todas as contas' },
  ...accounts.items.map((a) => ({ value: a.id as string | null, label: a.isActive ? a.name : `${a.name} (inativa)` })),
])

function clearAll(): void {
  debounced.cancel()
  searchText.value = ''
  emit('update:modelValue', {
    page: 1,
    pageSize: props.modelValue.pageSize,
    sort: props.modelValue.sort,
    // Mantém restrições estruturais da tela (ex.: conta fixa).
    accountId: props.hideAccount ? props.modelValue.accountId : undefined,
  })
}
</script>

<template>
  <section class="tx-filters" aria-label="Filtros de movimentações">
    <div class="tx-filters__top">
      <IconField class="tx-filters__search">
        <InputIcon class="pi pi-search" aria-hidden="true" />
        <InputText
          v-model="searchText"
          type="search"
          name="search"
          aria-label="Buscar por descrição"
          placeholder="Buscar por descrição"
          autocomplete="off"
          fluid
          @input="onSearchInput"
          @keydown.enter.prevent="debounced.flush(searchText)"
        />
      </IconField>
      <Button
        class="tx-filters__toggle"
        type="button"
        :label="activeCount > 0 ? `Filtros (${activeCount})` : 'Filtros'"
        icon="pi pi-filter"
        severity="secondary"
        outlined
        aria-controls="tx-filters-panel"
        :aria-expanded="expanded"
        @click="expanded = !expanded"
      />
    </div>

    <div id="tx-filters-panel" class="tx-filters__panel" :class="{ 'tx-filters__panel--open': expanded }">
      <div class="tx-filters__grid">
        <FormField id="filter-from" v-slot="{ id, describedBy, invalid }" label="De">
          <DateInput
            :id="id"
            :model-value="modelValue.from ?? null"
            :described-by="describedBy"
            :invalid="invalid"
            :max="modelValue.to ?? null"
            @update:model-value="update({ from: $event ?? undefined })"
          />
        </FormField>
        <FormField id="filter-to" v-slot="{ id, describedBy, invalid }" label="Até">
          <DateInput
            :id="id"
            :model-value="modelValue.to ?? null"
            :described-by="describedBy"
            :invalid="invalid"
            :min="modelValue.from ?? null"
            @update:model-value="update({ to: $event ?? undefined })"
          />
        </FormField>
        <FormField id="filter-type" v-slot="{ id }" label="Tipo">
          <Select
            :input-id="id"
            :model-value="modelValue.type ?? null"
            :options="typeOptions"
            option-label="label"
            option-value="value"
            fluid
            @update:model-value="
              update({
                type: $event ?? undefined,
                // Trocar o tipo invalida uma categoria de outro tipo.
                categoryId: undefined,
              })
            "
          />
        </FormField>
        <FormField id="filter-status" v-slot="{ id }" label="Situação">
          <Select
            :input-id="id"
            :model-value="modelValue.status ?? null"
            :options="statusOptions"
            option-label="label"
            option-value="value"
            fluid
            @update:model-value="update({ status: $event ?? undefined })"
          />
        </FormField>
        <FormField
          id="filter-category"
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
        <FormField v-if="!hideAccount" id="filter-account" v-slot="{ id }" label="Conta">
          <Select
            :input-id="id"
            :model-value="modelValue.accountId ?? null"
            :options="accountOptions"
            option-label="label"
            option-value="value"
            fluid
            @update:model-value="update({ accountId: $event ?? undefined })"
          />
        </FormField>
        <FormField id="filter-sort" v-slot="{ id }" label="Ordenar por">
          <Select
            :input-id="id"
            :model-value="modelValue.sort"
            :options="SORT_OPTIONS as unknown as { value: string; label: string }[]"
            option-label="label"
            option-value="value"
            fluid
            @update:model-value="update({ sort: $event })"
          />
        </FormField>
      </div>
      <div class="tx-filters__actions">
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
.tx-filters {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 1rem;
  padding: 1rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.tx-filters__top {
  display: flex;
  gap: 0.5rem;
}

.tx-filters__search {
  flex: 1;
}

.tx-filters__grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
  gap: 0.75rem 1rem;
}

.tx-filters__actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 0.5rem;
}

/* Em telas pequenas os filtros ficam recolhidos atrás do botão (nenhum filtro é removido). */
@media (max-width: 767px) {
  .tx-filters__panel:not(.tx-filters__panel--open) {
    display: none;
  }
}

@media (min-width: 768px) {
  .tx-filters__toggle {
    display: none;
  }
}
</style>
