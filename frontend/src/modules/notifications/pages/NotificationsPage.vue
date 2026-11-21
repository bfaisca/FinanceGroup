<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Paginator, { type PageState } from 'primevue/paginator'
import ToggleSwitch from 'primevue/toggleswitch'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import NotificationItem from '../components/NotificationItem.vue'
import { useNotificationsStore } from '../store'

const PAGE_SIZE = 20

const store = useNotificationsStore()
const unreadOnly = ref(false)
const page = ref(1)

const list = computed(() => store.full)

function load(): Promise<void> {
  return store.fetchPage({ page: page.value, pageSize: PAGE_SIZE, unreadOnly: unreadOnly.value })
}

function onToggle(value: boolean): void {
  unreadOnly.value = value
  page.value = 1
  void load()
}

function onPage(event: PageState): void {
  page.value = event.page + 1
  void load()
}

onMounted(() => {
  void store.fetchCount({ force: true })
  void load()
})
</script>

<template>
  <div>
    <PageHeader title="Notificações" description="Atividade dos seus grupos e convites recebidos.">
      <template #actions>
        <Button
          type="button"
          label="Marcar todas como lidas"
          icon="pi pi-check-circle"
          severity="secondary"
          outlined
          :disabled="store.unreadCount === 0"
          @click="store.markAllRead()"
        />
      </template>
    </PageHeader>

    <div class="notif-page__filter">
      <ToggleSwitch v-model="unreadOnly" input-id="notif-unread-only" @update:model-value="onToggle" />
      <label for="notif-unread-only">Somente não lidas</label>
    </div>

    <LoadingState v-if="list.loading && list.items.length === 0" label="Carregando notificações…" />

    <Message v-else-if="list.error && list.items.length === 0" severity="error" role="alert">
      <div class="notif-page__error">
        <span>{{ list.error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="load()" />
      </div>
    </Message>

    <EmptyState
      v-else-if="list.loaded && list.items.length === 0"
      :title="unreadOnly ? 'Nenhuma notificação não lida' : 'Nenhuma notificação'"
      :description="
        unreadOnly
          ? 'Você está em dia. Desative o filtro para ver as notificações anteriores.'
          : 'Quando houver movimentações, pagamentos ou convites nos seus grupos, eles aparecerão aqui.'
      "
      icon="pi pi-bell"
    />

    <template v-else>
      <Message v-if="list.error" severity="error" role="alert" class="notif-page__stale">
        Não foi possível atualizar: {{ list.error }}
      </Message>
      <ul class="surface-card notif-page__list" aria-label="Notificações" :aria-busy="list.loading">
        <NotificationItem v-for="item in list.items" :key="item.id" :notification="item" />
      </ul>
      <Paginator
        v-if="list.totalPages > 1"
        :rows="PAGE_SIZE"
        :total-records="list.totalCount"
        :first="(page - 1) * PAGE_SIZE"
        @page="onPage"
      />
    </template>
  </div>
</template>

<style scoped>
.notif-page__filter {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-bottom: 1rem;
}

.notif-page__list {
  margin: 0;
  padding: 0;
  overflow: hidden;
}

.notif-page__stale {
  margin-bottom: 1rem;
}

.notif-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
