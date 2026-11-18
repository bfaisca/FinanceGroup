<script setup lang="ts">
import type { NavItem } from '@/layouts/navigation'

withDefaults(
  defineProps<{
    items: readonly NavItem[]
    /** Modo compacto (somente ícones): o rótulo permanece acessível a leitores de tela. */
    compact?: boolean
  }>(),
  { compact: false },
)

const emit = defineEmits<{ navigate: [] }>()
</script>

<template>
  <ul class="app-nav" :class="{ 'app-nav--compact': compact }">
    <li v-for="item in items" :key="item.routeName">
      <RouterLink
        v-tooltip.right="compact ? item.label : undefined"
        :to="{ name: item.routeName }"
        class="app-nav__link"
        active-class="app-nav__link--active"
        @click="emit('navigate')"
      >
        <i :class="item.icon" class="app-nav__icon" aria-hidden="true" />
        <span class="app-nav__label" :class="{ 'sr-only': compact }">{{ item.label }}</span>
      </RouterLink>
    </li>
  </ul>
</template>

<style scoped>
.app-nav {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.app-nav__link {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-height: 44px;
  padding: 0.5rem 0.875rem;
  border-radius: 10px;
  color: var(--gf-text);
  text-decoration: none;
  font-weight: 500;
  transition: background-color 0.15s ease;
}

.app-nav__link:hover {
  background: var(--p-content-hover-background);
}

.app-nav__link--active {
  background: var(--p-highlight-background);
  color: var(--p-highlight-color);
  font-weight: 600;
}

.app-nav__icon {
  font-size: 1.125rem;
  width: 1.25rem;
  text-align: center;
  flex-shrink: 0;
}

.app-nav--compact .app-nav__link {
  justify-content: center;
  padding: 0.5rem;
}
</style>
