<script setup lang="ts">
import { computed } from 'vue'
import Skeleton from 'primevue/skeleton'
import { formatCurrency } from '@/utils/format'

/**
 * Card de indicador financeiro. Não exibe valor quando `value` é nulo (sem dados reais),
 * e indica explicitamente quando o valor é projetado (CLAUDE.md §7).
 */
const props = withDefaults(
  defineProps<{
    label: string
    value?: number | null
    currency?: string
    icon?: string
    tone?: 'neutral' | 'positive' | 'negative'
    projected?: boolean
    loading?: boolean
    caption?: string
  }>(),
  {
    value: null,
    currency: 'BRL',
    icon: undefined,
    tone: 'neutral',
    projected: false,
    loading: false,
    caption: undefined,
  },
)

const formatted = computed(() => formatCurrency(props.value, props.currency))
</script>

<template>
  <article class="financial-card" :class="`financial-card--${tone}`" :aria-busy="loading">
    <header class="financial-card__header">
      <span class="financial-card__label">{{ label }}</span>
      <i v-if="icon" :class="icon" class="financial-card__icon" aria-hidden="true" />
    </header>
    <Skeleton v-if="loading" height="2rem" width="60%" />
    <p v-else class="financial-card__value">{{ formatted }}</p>
    <footer v-if="projected || caption || $slots.default" class="financial-card__footer">
      <span v-if="projected" class="financial-card__badge">Projetado</span>
      <span v-if="caption">{{ caption }}</span>
      <slot />
    </footer>
  </article>
</template>

<style scoped>
.financial-card {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1.25rem;
  border-radius: var(--gf-radius);
  border: 1px solid var(--gf-border);
  background: var(--gf-surface);
}

.financial-card__header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
  font-weight: 500;
}

.financial-card__icon {
  font-size: 1.125rem;
}

.financial-card__value {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}

.financial-card--positive .financial-card__value {
  color: var(--gf-success);
}

.financial-card--negative .financial-card__value {
  color: var(--gf-danger);
}

.financial-card__footer {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.financial-card__badge {
  padding: 0.125rem 0.5rem;
  border-radius: 999px;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-weight: 600;
}
</style>
