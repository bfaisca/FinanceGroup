<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import { useTheme } from '@/composables/useTheme'

const { isDark, toggleTheme } = useTheme()
const label = computed(() => (isDark.value ? 'Ativar tema claro' : 'Ativar tema escuro'))
</script>

<template>
  <Button
    v-tooltip.bottom="label"
    :icon="isDark ? 'pi pi-sun' : 'pi pi-moon'"
    :aria-label="label"
    severity="secondary"
    text
    rounded
    @click="toggleTheme"
  />
</template>
