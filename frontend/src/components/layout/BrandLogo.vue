<script setup lang="ts">
withDefaults(defineProps<{ compact?: boolean }>(), { compact: false })
</script>

<template>
  <span class="brand">
    <span class="brand__mark" aria-hidden="true"><i class="pi pi-wallet" /></span>
    <span class="brand__name" :class="{ 'sr-only': compact }">GroupFinance</span>
  </span>
</template>

<style scoped>
.brand {
  display: inline-flex;
  align-items: center;
  gap: 0.625rem;
  font-weight: 700;
  font-size: 1.125rem;
  color: var(--gf-text);
}

.brand__mark {
  display: grid;
  place-items: center;
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 10px;
  background: var(--p-primary-color);
  color: var(--p-primary-contrast-color);
  flex-shrink: 0;
}
</style>
