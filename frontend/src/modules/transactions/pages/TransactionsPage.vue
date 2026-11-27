<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import PageHeader from '@/components/PageHeader.vue'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useClockStore } from '@/stores/clock'
import { useCreditCardsStore } from '@/stores/creditCards'
import { hasNoFilters, monthPeriod } from '@/utils/period'
import type { TransactionDetailDto, TransactionDto } from '@/types/api'
import TransactionDetailDrawer from '../components/TransactionDetailDrawer.vue'
import TransactionFiltersBar from '../components/TransactionFiltersBar.vue'
import TransactionFormDialog from '../components/TransactionFormDialog.vue'
import TransactionList from '../components/TransactionList.vue'
import { useTransactionList } from '../composables/useTransactionList'
import {
  countActiveFilters,
  mergeRouteQuery,
  parseTransactionFilters,
  toListQuery,
  type TransactionFilters,
} from '../filters'

const route = useRoute()
const router = useRouter()
const categories = useCategoriesStore()
const accounts = useAccountsStore()
const cards = useCreditCardsStore()

// A URL é a fonte dos filtros: links compartilháveis e navegação vindas do dashboard/contas.
// Sem nenhum filtro na URL, a tela abre no mês atual (a URL passa a refletir o período). Limpar as datas depois respeita a escolha.
const clock = useClockStore()
const defaultPeriod = computed(() => monthPeriod(clock.today))
const seedPeriod = ref(hasNoFilters(route.query, ['page', 'pageSize', 'sort', 'open', 'new']))
const filters = computed(() => {
  const parsed = parseTransactionFilters(route.query)
  return seedPeriod.value ? { ...parsed, ...defaultPeriod.value } : parsed
})
const list = useTransactionList(() => toListQuery(filters.value))

function setFilters(next: TransactionFilters): void {
  void router.replace({ query: mergeRouteQuery(route.query, next) })
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

function openDetail(tx: TransactionDto): void {
  void router.replace({ query: { ...route.query, open: tx.id } })
}

// ---- Formulário ----
const formVisible = ref(false)
const editing = ref<TransactionDetailDto | null>(null)

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(transaction: TransactionDetailDto): void {
  editing.value = transaction
  formVisible.value = true
}

function onSaved(): void {
  void list.reload()
  refreshToken.value += 1
}

// `?new=1` (atalhos do dashboard/estado vazio) abre o formulário uma vez e limpa o parâmetro.
function consumeNewParam(): void {
  if (route.query.new === undefined) return
  const { new: _new, ...rest } = route.query
  void router.replace({ query: rest })
  openCreate()
}

onMounted(() => {
  if (seedPeriod.value) {
    // Só deixa de semear depois que a URL já tem o período (evita uma consulta intermediária sem datas).
    void router.replace({ query: { ...route.query, ...defaultPeriod.value } }).finally(() => {
      seedPeriod.value = false
    })
  }
  void categories.load()
  void accounts.load()
  void cards.load()
  consumeNewParam()
})

watch(() => route.query.new, consumeNewParam)

// Se a página atual deixou de existir (ex.: após excluir o último item dela), volta à última.
watch(
  () => [list.totalPages.value, list.loading.value] as const,
  ([totalPages, loading]) => {
    if (!loading && totalPages > 0 && filters.value.page > totalPages) {
      setFilters({ ...filters.value, page: totalPages })
    }
  },
)

// O mês atual (período padrão) não conta como filtro: sem outros filtros, a lista vazia continua sendo "sem movimentações".
const hasFilters = computed(() => {
  const isDefault = filters.value.from === defaultPeriod.value.from && filters.value.to === defaultPeriod.value.to
  return countActiveFilters(isDefault ? { ...filters.value, from: undefined, to: undefined } : filters.value) > 0
})
</script>

<template>
  <div>
    <PageHeader title="Movimentações" description="Receitas e despesas, com filtros, pagamentos e histórico.">
      <template #actions>
        <Button type="button" label="Nova movimentação" icon="pi pi-plus" @click="openCreate" />
      </template>
    </PageHeader>

    <TransactionFiltersBar :model-value="filters" :default-period="defaultPeriod" @update:model-value="setFilters" />

    <p v-if="list.loadedOnce.value && !list.error.value" class="tx-page__count" aria-live="polite">
      {{ list.totalCount.value }} {{ list.totalCount.value === 1 ? 'movimentação' : 'movimentações' }}
    </p>

    <TransactionList
      :items="list.items.value"
      :loading="list.loading.value"
      :error="list.error.value"
      :total-count="list.totalCount.value"
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
        <Button v-if="!hasFilters" type="button" label="Nova movimentação" icon="pi pi-plus" @click="openCreate" />
      </template>
    </TransactionList>

    <TransactionDetailDrawer
      v-model:visible="detailVisible"
      :transaction-id="openId"
      :refresh-token="refreshToken"
      @changed="list.reload()"
      @edit="openEdit"
    />
    <TransactionFormDialog v-model:visible="formVisible" :transaction="editing" @saved="onSaved" />
  </div>
</template>

<style scoped>
.tx-page__count {
  margin: 0 0 0.75rem;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}
</style>
