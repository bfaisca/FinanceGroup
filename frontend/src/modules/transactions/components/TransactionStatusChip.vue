<script setup lang="ts">
import { computed } from 'vue'
import type { TransactionStatus } from '@/types/api'
import { STATUS_PRESENTATION } from '@/utils/labels'

/** Chip de situação: cor + ícone + texto (a cor nunca é o único indicador). */
const props = defineProps<{ status: TransactionStatus }>()

const presentation = computed(() => STATUS_PRESENTATION[props.status])
</script>

<template>
  <span class="status-chip" :class="`status-chip--${presentation.tone}`" data-testid="status-chip">
    <i :class="presentation.icon" aria-hidden="true" />
    {{ presentation.label }}
  </span>
</template>

<style scoped>
.status-chip {
  --chip: var(--gf-neutral);
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  padding: 0.125rem 0.625rem;
  border-radius: 999px;
  border: 1px solid color-mix(in srgb, var(--chip) 45%, transparent);
  background: color-mix(in srgb, var(--chip) 12%, transparent);
  color: var(--chip);
  font-size: 0.8125rem;
  font-weight: 600;
  line-height: 1.5;
  white-space: nowrap;
}

.status-chip i {
  font-size: 0.75rem;
}

.status-chip--info {
  --chip: var(--gf-info);
}

.status-chip--warning {
  --chip: var(--gf-warning);
}

.status-chip--success {
  --chip: var(--gf-success);
}

.status-chip--danger {
  --chip: var(--gf-danger);
}
</style>
