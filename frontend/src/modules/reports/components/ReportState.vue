<script setup lang="ts">
import { computed } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'

/**
 * Estados de um relatório: carregando, erro (com "Tentar novamente"), vazio e conteúdo.
 * Mantém o conteúdo anterior visível durante recargas; erros após a primeira carga viram aviso.
 */
const props = withDefaults(
  defineProps<{
    loading: boolean
    error: string | null
    /** Há dados carregados (`data !== null`). */
    loaded: boolean
    empty?: boolean
    emptyTitle?: string
    emptyDescription?: string
    loadingLabel?: string
  }>(),
  {
    empty: false,
    emptyTitle: 'Sem dados para exibir',
    emptyDescription: 'Não há movimentações para este relatório no período selecionado.',
    loadingLabel: 'Carregando relatório…',
  },
)

const emit = defineEmits<{ retry: [] }>()

const showError = computed(() => props.error !== null)
</script>

<template>
  <div :aria-busy="loading" class="report-state">
    <Message v-if="showError" severity="error" role="alert" data-testid="report-error">
      <div class="report-state__error">
        <span>{{ error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="emit('retry')" />
      </div>
    </Message>

    <LoadingState v-if="loading && !loaded && !showError" :label="loadingLabel" />

    <EmptyState
      v-else-if="loaded && empty"
      icon="pi pi-chart-bar"
      :title="emptyTitle"
      :description="emptyDescription"
    />

    <slot v-else-if="loaded" />
  </div>
</template>

<style scoped>
.report-state {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.report-state__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
