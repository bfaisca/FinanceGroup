<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import SelectButton from 'primevue/selectbutton'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useCategoriesStore } from '@/stores/categories'
import type { CategoryDto, CategoryType } from '@/types/api'
import { CATEGORY_TYPE_LABELS, enumOptions } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import CategoryFormDialog from '../components/CategoryFormDialog.vue'
import CategoryTree from '../components/CategoryTree.vue'

const categories = useCategoriesStore()
const toast = useToast()

const activeType = ref<CategoryType>('Expense')
const showInactive = ref(false)
const typeOptions = enumOptions({ Expense: 'Despesas', Income: 'Receitas' } satisfies Record<CategoryType, string>)

const formVisible = ref(false)
const editing = ref<CategoryDto | null>(null)
const presetParentId = ref<string | null>(null)

const confirmVisible = ref(false)
const toggling = ref<CategoryDto | null>(null)
const busy = ref(false)

const visibleItems = computed(() => categories.items.filter((c) => showInactive.value || c.isActive))
const hasOfType = computed(() => visibleItems.value.some((c) => c.type === activeType.value))
const hasInactive = computed(() => categories.items.some((c) => !c.isActive))
const childCount = computed(() =>
  toggling.value ? categories.items.filter((c) => c.parentCategoryId === toggling.value?.id && c.isActive).length : 0,
)

onMounted(() => {
  void categories.load(true)
})

function openCreate(): void {
  editing.value = null
  presetParentId.value = null
  formVisible.value = true
}

function openAddSub(parent: CategoryDto): void {
  editing.value = null
  presetParentId.value = parent.id
  formVisible.value = true
}

function openEdit(category: CategoryDto): void {
  editing.value = category
  presetParentId.value = null
  formVisible.value = true
}

async function runToggle(category: CategoryDto): Promise<void> {
  busy.value = true
  try {
    if (category.isActive) await categories.deactivate(category.id)
    else await categories.activate(category.id)
    toast.add({
      severity: 'success',
      summary: category.isActive ? 'Categoria desativada' : 'Categoria reativada',
      life: 3500,
    })
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível concluir',
      detail: toAppError(error).message,
      life: 6000,
    })
  } finally {
    confirmVisible.value = false
    busy.value = false
  }
}

function onToggle(category: CategoryDto): void {
  if (category.isActive) {
    toggling.value = category
    confirmVisible.value = true
  } else {
    void runToggle(category)
  }
}

const confirmMessage = computed(() => {
  const name = toggling.value?.name ?? ''
  const base = `Desativar “${name}”? Ela deixa de poder ser usada em novas movimentações; as movimentações existentes não são alteradas.`
  return childCount.value > 0 || toggling.value?.parentCategoryId === null
    ? `${base} As subcategorias também serão desativadas.`
    : base
})
</script>

<template>
  <div>
    <PageHeader title="Categorias" description="Categorias padrão e personalizadas, com subcategorias.">
      <template #actions>
        <Button type="button" label="Nova categoria" icon="pi pi-plus" @click="openCreate" />
      </template>
    </PageHeader>

    <div class="categories-page__toolbar">
      <SelectButton
        v-model="activeType"
        :options="typeOptions"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        aria-label="Tipo de categoria"
      />
      <div v-if="hasInactive" class="categories-page__filter">
        <Checkbox v-model="showInactive" input-id="show-inactive-categories" binary />
        <label for="show-inactive-categories">Mostrar inativas</label>
      </div>
    </div>

    <LoadingState v-if="categories.loading && !categories.loaded" label="Carregando categorias…" />

    <Message v-else-if="categories.error && !categories.loaded" severity="error" role="alert">
      <div class="categories-page__error">
        <span>{{ categories.error }}</span>
        <Button
          type="button"
          label="Tentar novamente"
          icon="pi pi-refresh"
          size="small"
          @click="categories.load(true)"
        />
      </div>
    </Message>

    <EmptyState
      v-else-if="!hasOfType"
      icon="pi pi-tags"
      :title="`Nenhuma categoria de ${CATEGORY_TYPE_LABELS[activeType].toLowerCase()}`"
      description="Crie uma categoria para organizar suas movimentações."
    >
      <Button type="button" label="Nova categoria" icon="pi pi-plus" @click="openCreate" />
    </EmptyState>

    <CategoryTree
      v-else
      :items="visibleItems"
      :type="activeType"
      :busy="busy"
      @add-sub="openAddSub"
      @edit="openEdit"
      @toggle="onToggle"
    />

    <CategoryFormDialog
      v-model:visible="formVisible"
      :category="editing"
      :preset-type="activeType"
      :preset-parent-id="presetParentId"
    />

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      header="Desativar categoria"
      :message="confirmMessage"
      confirm-label="Desativar"
      severity="danger"
      :loading="busy"
      @confirm="toggling && runToggle(toggling)"
    />
  </div>
</template>

<style scoped>
.categories-page__toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.categories-page__filter {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.categories-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
