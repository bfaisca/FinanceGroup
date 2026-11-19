<script setup lang="ts">
import EmptyState from '@/components/EmptyState.vue'
import TransactionAmount from '@/modules/transactions/components/TransactionAmount.vue'
import TransactionStatusChip from '@/modules/transactions/components/TransactionStatusChip.vue'
import type { TransactionDto } from '@/types/api'
import { formatDate } from '@/utils/format'
import { describeDue } from '../summary'

defineProps<{ items: TransactionDto[]; today: string }>()
const emit = defineEmits<{ open: [transaction: TransactionDto] }>()
</script>

<template>
  <EmptyState
    v-if="items.length === 0"
    icon="pi pi-calendar"
    title="Nenhum vencimento próximo"
    description="Movimentações com vencimento nos próximos 30 dias, ou atrasadas, aparecem aqui."
  />
  <ul v-else class="dues" aria-label="Próximos vencimentos">
    <li v-for="tx in items" :key="tx.id">
      <button
        type="button"
        class="dues__item"
        :class="{ 'dues__item--overdue': tx.status === 'Overdue' }"
        @click="emit('open', tx)"
      >
        <span class="dues__main">
          <span class="dues__title">{{ tx.description }}</span>
          <span class="dues__meta">
            {{ formatDate(tx.dueDate) }} · {{ tx.dueDate ? describeDue(tx.dueDate, today) : '' }}
          </span>
        </span>
        <span class="dues__side">
          <TransactionAmount :amount="tx.remainingAmount" :type="tx.type" />
          <TransactionStatusChip :status="tx.status" />
        </span>
      </button>
    </li>
  </ul>
</template>

<style scoped>
.dues {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.dues__item {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid var(--gf-border);
  border-left-width: 4px;
  border-radius: 10px;
  background: var(--gf-surface);
  color: inherit;
  font: inherit;
  text-align: left;
  cursor: pointer;
}

.dues__item:hover {
  background: var(--p-content-hover-background);
}

.dues__item--overdue {
  border-left-color: var(--gf-danger);
}

.dues__main,
.dues__side {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.dues__side {
  align-items: flex-end;
}

.dues__title {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.dues__meta {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}
</style>
