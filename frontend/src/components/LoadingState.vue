<script setup lang="ts">
import ProgressSpinner from 'primevue/progressspinner'

withDefaults(defineProps<{ label?: string; fullscreen?: boolean }>(), {
  label: 'Carregando…',
  fullscreen: false,
})
</script>

<template>
  <div class="loading-state" :class="{ 'loading-state--fullscreen': fullscreen }" role="status" aria-live="polite">
    <ProgressSpinner style="width: 2.5rem; height: 2.5rem" stroke-width="4" aria-hidden="true" />
    <span class="loading-state__label">{{ label }}</span>
  </div>
</template>

<style scoped>
.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  padding: 2rem;
  color: var(--gf-text-muted);
}

.loading-state--fullscreen {
  min-height: 100vh;
}
</style>
