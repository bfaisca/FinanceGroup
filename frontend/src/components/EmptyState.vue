<script setup lang="ts">
withDefaults(
  defineProps<{
    title: string
    description?: string
    /** Classe PrimeIcons, ex.: "pi pi-inbox". */
    icon?: string
  }>(),
  { description: undefined, icon: 'pi pi-inbox' },
)
</script>

<template>
  <section class="empty-state" :aria-label="title">
    <span class="empty-state__icon" aria-hidden="true"><i :class="icon" /></span>
    <h2 class="empty-state__title">{{ title }}</h2>
    <p v-if="description" class="empty-state__description">{{ description }}</p>
    <div v-if="$slots.default" class="empty-state__actions">
      <slot />
    </div>
  </section>
</template>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 0.5rem;
  padding: 3rem 1.5rem;
  border: 1px dashed var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.empty-state__icon {
  display: grid;
  place-items: center;
  width: 3.5rem;
  height: 3.5rem;
  border-radius: 50%;
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-size: 1.5rem;
  margin-bottom: 0.5rem;
}

.empty-state__title {
  font-size: 1.125rem;
  font-weight: 600;
}

.empty-state__description {
  margin: 0;
  max-width: 36rem;
  color: var(--gf-text-muted);
}

.empty-state__actions {
  margin-top: 1rem;
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
  justify-content: center;
}
</style>
