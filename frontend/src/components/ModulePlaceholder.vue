<script setup lang="ts">
import PageHeader from './PageHeader.vue'
import EmptyState from './EmptyState.vue'

/** Página de módulo ainda não implementado: estado vazio honesto, sem dados simulados. */
defineProps<{
  title: string
  description?: string
  icon?: string
}>()
</script>

<template>
  <div>
    <PageHeader :title="title" :description="description" />
    <EmptyState
      :icon="icon"
      title="Disponível na próxima etapa"
      description="Esta funcionalidade ainda está em desenvolvimento e será liberada em breve."
    />
  </div>
</template>
