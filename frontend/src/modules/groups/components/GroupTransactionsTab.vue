<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useCategoriesStore } from '@/stores/categories'
import { useResource } from '../composables'
import {
  countActiveGroupFilters,
  mergeGroupRouteQuery,
  parseGroupTransactionFilters,
  toGroupListQuery,
  type GroupTransactionFilters,
} from '../filters'
import { useGroupRefreshHandler } from '../refresh'
import { groupService } from '../services/groupService'
import { groupTransactionService } from '../services/groupTransactionService'
import type { GroupDetailDto, GroupTransactionDetailDto, GroupTransactionDto } from '../types'
import GroupTransactionDetailDrawer from './GroupTransactionDetailDrawer.vue'
import GroupTransactionFiltersBar from './GroupTransactionFiltersBar.vue'
import GroupTransactionFormDialog from './GroupTransactionFormDialog.vue'
import GroupTransactionList from './GroupTransactionList.vue'

const props = defineProps<{ group: GroupDetailDto }>()

const route = useRoute()
const router = useRouter()
const categories = useCategoriesStore()

// A URL é a fonte dos filtros: links compartilháveis e recarregar a página preservam a visão.
const filters = computed(() => parseGroupTransactionFilters(route.query))
const list = useResource(
  () => groupTransactionService.list(props.group.id, toGroupListQuery(filters.value)),
  () => `${props.group.id}|${JSON.stringify(toGroupListQuery(filters.value))}`,
)

// Inclui ex-membros: é possível filtrar por quem já saiu do grupo.
const members = useResource(
  () => groupService.listMembers(props.group.id, true),
  () => props.group.id,
)

const items = computed(() => list.data.value?.items ?? [])
const totalCount = computed(() => list.data.value?.totalCount ?? 0)
const totalPages = computed(() => list.data.value?.totalPages ?? 0)
const hasFilters = computed(() => countActiveGroupFilters(filters.value) > 0)
const canCreate = computed(() => props.group.permissions.canCreateTransactions)

function setFilters(next: GroupTransactionFilters): void {
  void router.replace({ query: mergeGroupRouteQuery(route.query, next) })
}

// ---- Detalhe (drawer) controlado por `?open=<id>` ----
const openId = computed(() => {
  const value = Array.isArray(route.query.open) ? route.query.open[0] : route.query.open
  return typeof value === 'string' && value.length > 0 ? value : null
})
const detailVisible = computed({
  get: () => openId.value !== null,
  set: (value: boolean) => {
    if (!value) {
      const { open: _open, ...rest } = route.query
      void router.replace({ query: rest })
    }
  },
})
const refreshToken = ref(0)

useGroupRefreshHandler(async () => {
  await Promise.all([list.reload(), members.reload()])
  // O detalhe aberto (drawer) também é recarregado.
  refreshToken.value += 1
  return list.error.value === null
})

function openDetail(tx: GroupTransactionDto): void {
  void router.replace({ query: { ...route.query, open: tx.id } })
}

// ---- Formulário ----
const formVisible = ref(false)
const editing = ref<GroupTransactionDetailDto | null>(null)

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(transaction: GroupTransactionDetailDto): void {
  editing.value = transaction
  formVisible.value = true
}

function onSaved(): void {
  void list.reload()
  refreshToken.value += 1
}

onMounted(() => {
  void categories.load()
})

// Se a página atual deixou de existir (ex.: após excluir o último item dela), volta à última.
watch(
  () => [totalPages.value, list.loading.value] as const,
  ([pages, loading]) => {
    if (!loading && pages > 0 && filters.value.page > pages) setFilters({ ...filters.value, page: pages })
  },
)
</script>

<template>
  <div>
    <div class="gtx-tab__header">
      <p class="gtx-tab__count" aria-live="polite">
        <template v-if="list.loadedOnce.value && !list.error.value">
          {{ totalCount }} {{ totalCount === 1 ? 'movimentação' : 'movimentações' }}
        </template>
      </p>
      <Button v-if="canCreate" type="button" label="Nova movimentação" icon="pi pi-plus" @click="openCreate" />
    </div>

    <Message
      v-if="!group.isActive"
      :pt="NOTE_PT"
      severity="info"
      :closable="false"
      class="gtx-tab__archived"
      data-testid="archived-note"
    >
      Grupo arquivado: as movimentações estão somente para leitura.
    </Message>

    <GroupTransactionFiltersBar
      :model-value="filters"
      :members="members.data.value ?? []"
      @update:model-value="setFilters"
    />

    <GroupTransactionList
      :items="items"
      :loading="list.loading.value"
      :error="list.error.value"
      :total-count="totalCount"
      :page="filters.page"
      :page-size="filters.pageSize"
      :sort="filters.sort"
      :filtered="hasFilters"
      @open="openDetail"
      @retry="list.reload()"
      @update:page="setFilters({ ...filters, page: $event })"
      @update:page-size="setFilters({ ...filters, pageSize: $event, page: 1 })"
      @update:sort="setFilters({ ...filters, sort: $event, page: 1 })"
    >
      <template #empty-actions>
        <Button
          v-if="canCreate && !hasFilters"
          type="button"
          label="Nova movimentação"
          icon="pi pi-plus"
          @click="openCreate"
        />
      </template>
    </GroupTransactionList>

    <GroupTransactionDetailDrawer
      v-model:visible="detailVisible"
      :group-id="group.id"
      :transaction-id="openId"
      :refresh-token="refreshToken"
      @changed="list.reload()"
      @edit="openEdit"
    />
    <GroupTransactionFormDialog
      v-model:visible="formVisible"
      :group-id="group.id"
      :transaction="editing"
      @saved="onSaved"
    />
  </div>
</template>

<style scoped>
.gtx-tab__header {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.gtx-tab__count {
  margin: 0;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}

.gtx-tab__archived {
  margin-bottom: 1rem;
}
</style>
