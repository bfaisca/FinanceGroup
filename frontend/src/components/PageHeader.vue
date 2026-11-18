<script setup lang="ts">
defineProps<{
  title: string
  description?: string
}>()
</script>

<template>
  <header class="page-header">
    <div class="page-header__text">
      <h1 class="page-header__title">{{ title }}</h1>
      <p v-if="description" class="page-header__description">{{ description }}</p>
    </div>
    <div v-if="$slots.actions" class="page-header__actions">
      <slot name="actions" />
    </div>
  </header>
</template>

<style scoped>
.page-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.page-header__title {
  font-size: 1.5rem;
  font-weight: 700;
}

.page-header__description {
  margin: 0.25rem 0 0;
  color: var(--gf-text-muted);
}

.page-header__actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}
</style>
