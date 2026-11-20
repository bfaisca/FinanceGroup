<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import GroupCard from '../components/GroupCard.vue'
import GroupFormDialog from '../components/GroupFormDialog.vue'
import { useResource } from '../composables'
import { groupService } from '../services/groupService'
import type { GroupDetailDto } from '../types'

const route = useRoute()
const router = useRouter()
const toast = useToast()

// "Mostrar arquivados" fica na URL (`?archived=1`): recarregar a página preserva a visão.
const includeArchived = computed(() => route.query.archived === '1')
const list = useResource(
  () => groupService.list(includeArchived.value),
  () => `archived:${includeArchived.value}`,
)

const groups = computed(() => list.data.value ?? [])
const showInitialLoading = computed(() => list.loading.value && !list.loadedOnce.value)

function setIncludeArchived(value: boolean): void {
  const { archived: _archived, ...rest } = route.query
  void router.replace({ query: value ? { ...rest, archived: '1' } : rest })
}

const formVisible = ref(false)

function onCreated(group: GroupDetailDto): void {
  toast.add({
    severity: 'success',
    summary: 'Grupo criado',
    detail: `Você é o proprietário de “${group.name}”.`,
    life: 4000,
  })
  void router.push({ name: 'group-detail', params: { id: group.id } })
}
</script>

<template>
  <div>
    <PageHeader
      title="Grupos"
      description="Transparência e visão consolidada das movimentações compartilhadas com outras pessoas."
    >
      <template #actions>
        <Button type="button" label="Novo grupo" icon="pi pi-plus" @click="formVisible = true" />
      </template>
    </PageHeader>

    <div class="groups-page__filter">
      <Checkbox
        :model-value="includeArchived"
        input-id="groups-archived"
        binary
        @update:model-value="setIncludeArchived(Boolean($event))"
      />
      <label for="groups-archived">Mostrar grupos arquivados</label>
    </div>

    <LoadingState v-if="showInitialLoading" label="Carregando grupos…" />

    <Message v-else-if="list.error.value && !list.data.value" severity="error" role="alert">
      <div class="groups-page__error">
        <span>{{ list.error.value }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="list.reload()" />
      </div>
    </Message>

    <EmptyState
      v-else-if="groups.length === 0"
      icon="pi pi-users"
      :title="includeArchived ? 'Nenhum grupo encontrado' : 'Você ainda não participa de nenhum grupo'"
      description="Grupos servem para dar transparência: todos os participantes veem as movimentações compartilhadas e um resumo consolidado. Eles NÃO dividem dívidas — não há acertos, cobranças nem saldos devedores entre as pessoas. Seu controle financeiro individual continua privado."
    >
      <Button type="button" label="Criar grupo" icon="pi pi-plus" @click="formVisible = true" />
    </EmptyState>

    <ul v-else class="groups-page__grid" aria-label="Seus grupos" :aria-busy="list.loading.value">
      <li v-for="group in groups" :key="group.id"><GroupCard :group="group" /></li>
    </ul>

    <GroupFormDialog v-model:visible="formVisible" @saved="onCreated" />
  </div>
</template>

<style scoped>
.groups-page__filter {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.groups-page__grid {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 18rem), 1fr));
  gap: 1rem;
}

.groups-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
