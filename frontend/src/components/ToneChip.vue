<script setup lang="ts">
/** Chip genérico: cor + ícone + texto (a cor nunca é o único indicador). */
withDefaults(
  defineProps<{ label: string; icon?: string; tone?: 'neutral' | 'info' | 'warning' | 'success' | 'danger' }>(),
  { icon: undefined, tone: 'neutral' },
)
</script>

<template>
  <span class="tone-chip" :class="`tone-chip--${tone}`">
    <i v-if="icon" :class="icon" aria-hidden="true" />
    {{ label }}
  </span>
</template>

<style scoped>
.tone-chip {
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

.tone-chip i {
  font-size: 0.75rem;
}

.tone-chip--info {
  --chip: var(--gf-info);
}

.tone-chip--warning {
  --chip: var(--gf-warning);
}

.tone-chip--success {
  --chip: var(--gf-success);
}

.tone-chip--danger {
  --chip: var(--gf-danger);
}
</style>
