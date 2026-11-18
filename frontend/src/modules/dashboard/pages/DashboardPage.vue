<script setup lang="ts">
import { computed } from 'vue'
import PageHeader from '@/components/PageHeader.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const firstName = computed(() => auth.user?.name.trim().split(/\s+/)[0] ?? '')
</script>

<template>
  <div>
    <PageHeader :title="firstName ? `Olá, ${firstName}` : 'Dashboard'" description="Visão geral das suas finanças." />
    <EmptyState
      icon="pi pi-chart-line"
      title="Disponível na próxima etapa"
      description="O resumo de saldos, receitas e despesas aparecerá aqui assim que o cadastro de contas e movimentações estiver disponível."
    />
  </div>
</template>
