<script setup lang="ts">
import { computed } from 'vue'
import { formatDateTime } from '@/utils/format'
import { describeHistoryEntry } from '@/utils/history'
import type { GroupTransactionHistoryEntryDto } from '../types'

const props = defineProps<{ entries: GroupTransactionHistoryEntryDto[] }>()

const rows = computed(() =>
  props.entries.map((entry) => ({
    id: entry.id,
    actor: entry.actorName,
    when: formatDateTime(entry.createdAt),
    text: describeHistoryEntry(entry),
  })),
)
</script>

<template>
  <ol class="history" aria-label="Histórico de alterações">
    <li v-for="row in rows" :key="row.id" class="history__item">
      <span class="history__text">{{ row.text }}</span>
      <span class="history__meta">
        por <strong>{{ row.actor }}</strong> ·
        <time>{{ row.when }}</time>
      </span>
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

.history__meta {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}
</style>
