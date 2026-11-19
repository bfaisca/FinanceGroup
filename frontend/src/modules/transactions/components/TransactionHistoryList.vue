<script setup lang="ts">
import { computed } from 'vue'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import type { TransactionHistoryEntryDto } from '@/types/api'
import { formatDateTime } from '@/utils/format'
import { describeHistoryEntry } from '@/utils/history'

const props = defineProps<{ entries: TransactionHistoryEntryDto[] }>()

const categories = useCategoriesStore()
const accounts = useAccountsStore()

const rows = computed(() =>
  props.entries.map((entry) => ({
    id: entry.id,
    when: formatDateTime(entry.createdAt),
    text: describeHistoryEntry(entry, {
      category: (id) => categories.nameOf(id),
      account: (id) => accounts.nameOf(id),
    }),
  })),
)
</script>

<template>
  <ol class="history" aria-label="Histórico de alterações">
    <li v-for="row in rows" :key="row.id" class="history__item">
      <span class="history__text">{{ row.text }}</span>
      <time class="history__time">{{ row.when }}</time>
    </li>
  </ol>
</template>

<style scoped>
.history {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
}

.history__item {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  padding: 0.75rem 0 0.75rem 1rem;
  border-left: 2px solid var(--gf-border);
}

.history__time {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}
</style>
