<script setup lang="ts">
import { ref, watch } from 'vue'
import Button from 'primevue/button'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import TransactionAmount from '@/modules/transactions/components/TransactionAmount.vue'
import TransactionStatusChip from '@/modules/transactions/components/TransactionStatusChip.vue'
import { transactionService } from '@/modules/transactions/services/transactionService'
import type { TransactionDto } from '@/types/api'
import { formatDate } from '@/utils/format'
import { toAppError } from '@/utils/problem'

/** Últimos registros lançados (independente do período do dashboard). */
const RECENT_LIMIT = 5

const props = defineProps<{ refreshKey?: string }>()
const emit = defineEmits<{ open: [transaction: TransactionDto] }>()

const items = ref<TransactionDto[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const page = await transactionService.list({ page: 1, pageSize: RECENT_LIMIT, sort: '-createdAt' })
    items.value = page.items
  } catch (e) {
    error.value = toAppError(e).message
  } finally {
    loading.value = false
  }
}

watch(() => props.refreshKey, load, { immediate: true })
</script>

<template>
  <LoadingState v-if="loading && items.length === 0" label="Carregando movimentações…" />
  <p v-else-if="error" class="recent__error" role="alert">
    {{ error }}
    <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" text @click="load" />
  </p>
  <EmptyState
    v-else-if="items.length === 0"
    icon="pi pi-arrow-right-arrow-left"
    title="Nenhuma movimentação ainda"
    description="Os últimos registros lançados aparecem aqui."
  />
  <ul v-else class="recent" aria-label="Últimas movimentações">
    <li v-for="tx in items" :key="tx.id">
      <button type="button" class="recent__item" @click="emit('open', tx)">
        <span class="recent__main">
          <span class="recent__title">{{ tx.description }}</span>
          <span class="recent__meta">
            {{ formatDate(tx.transactionDate) }} ·
            {{ tx.groupPayment ? `Grupo: ${tx.groupPayment.groupName}` : tx.categoryName }}
          </span>
        </span>
        <span class="recent__side">
          <TransactionAmount :amount="tx.amount" :type="tx.type" />
          <TransactionStatusChip :status="tx.status" />
        </span>
      </button>
    </li>
  </ul>
</template>

<style scoped>
.recent {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.recent__item {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid var(--gf-border);
  border-radius: 10px;
  background: var(--gf-surface);
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.recent__item:hover {
  background: var(--p-content-hover-background);
}

.recent__main,
.recent__side {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.recent__side {
  align-items: flex-end;
}

.recent__title {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.recent__meta {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.recent__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  margin: 0;
}
</style>
