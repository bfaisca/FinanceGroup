<script setup lang="ts">
import { computed } from 'vue'

/** Nome da categoria com ícone sobre fundo da cor da categoria (o nome sempre aparece em texto). */
const props = withDefaults(
  defineProps<{
    name: string
    parentName?: string | null
    color?: string | null
    icon?: string | null
  }>(),
  { parentName: null, color: null, icon: null },
)

const label = computed(() => (props.parentName ? `${props.parentName} › ${props.name}` : props.name))
const iconClass = computed(() => {
  if (!props.icon) return 'pi pi-tag'
  return props.icon.startsWith('pi ') ? props.icon : `pi ${props.icon}`
})
const style = computed(() => (props.color ? { '--badge-color': props.color } : undefined))
</script>

<template>
  <span class="category-badge" :style="style">
    <span class="category-badge__icon" aria-hidden="true"><i :class="iconClass" /></span>
    <span class="category-badge__label">{{ label }}</span>
  </span>
</template>

<style scoped>
.category-badge {
  --badge-color: var(--gf-neutral);
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
}

.category-badge__icon {
  display: grid;
  place-items: center;
  flex: none;
  width: 1.75rem;
  height: 1.75rem;
  border-radius: 50%;
  background: color-mix(in srgb, var(--badge-color) 20%, transparent);
  color: var(--badge-color);
  font-size: 0.8125rem;
}

.category-badge__label {
  overflow-wrap: anywhere;
}
</style>
