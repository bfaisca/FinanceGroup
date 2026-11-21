<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { formatBadge } from '../messages'
import { useNotificationsStore } from '../store'
import NotificationItem from './NotificationItem.vue'

const store = useNotificationsStore()
const route = useRoute()

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const panel = ref<HTMLElement | null>(null)
const panelId = 'notifications-panel'

const count = computed(() => store.unreadCount)
const badge = computed(() => formatBadge(count.value))
const label = computed(() => {
  if (count.value === 0) return 'Notificações, nenhuma não lida'
  return `Notificações, ${count.value} ${count.value === 1 ? 'não lida' : 'não lidas'}`
})
const recent = computed(() => store.recent)

async function show(): Promise<void> {
  open.value = true
  void store.fetchCount({ force: true })
  void store.fetchRecent()
  await nextTick()
  panel.value?.focus()
}

function hide(returnFocus = false): void {
  if (!open.value) return
  open.value = false
  if (returnFocus) void nextTick(() => trigger.value?.focus())
}

function toggle(): void {
  if (open.value) hide()
  else void show()
}

async function markAll(): Promise<void> {
  await store.markAllRead()
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && open.value) {
    event.stopPropagation()
    hide(true)
  }
}

function onPointerDown(event: Event): void {
  if (open.value && root.value && !root.value.contains(event.target as Node)) hide()
}

function onFocusIn(event: FocusEvent): void {
  if (open.value && root.value && event.target instanceof Node && !root.value.contains(event.target)) hide()
}

onMounted(() => {
  document.addEventListener('pointerdown', onPointerDown)
  document.addEventListener('focusin', onFocusIn)
})
onBeforeUnmount(() => {
  document.removeEventListener('pointerdown', onPointerDown)
  document.removeEventListener('focusin', onFocusIn)
})

watch(
  () => route.fullPath,
  () => hide(),
)
</script>

<template>
  <div ref="root" class="notif-bell" @keydown="onKeydown">
    <button
      ref="trigger"
      type="button"
      class="notif-bell__trigger"
      :aria-label="label"
      aria-haspopup="dialog"
      :aria-expanded="open"
      :aria-controls="panelId"
      data-testid="notification-bell"
      @click="toggle"
    >
      <i class="pi pi-bell" aria-hidden="true" />
      <span v-if="count > 0" class="notif-bell__badge" aria-hidden="true" data-testid="notification-badge">{{
        badge
      }}</span>
    </button>

    <section
      v-if="open"
      :id="panelId"
      ref="panel"
      class="notif-panel"
      role="dialog"
      aria-label="Notificações"
      tabindex="-1"
      data-testid="notification-panel"
    >
      <header class="notif-panel__header">
        <h2 class="notif-panel__title">Notificações</h2>
        <Button
          type="button"
          label="Marcar todas como lidas"
          size="small"
          severity="secondary"
          text
          :disabled="count === 0"
          @click="markAll"
        />
      </header>

      <div class="notif-panel__body">
        <p v-if="recent.loading && recent.items.length === 0" class="notif-panel__state" role="status">
          Carregando notificações…
        </p>
        <Message v-else-if="recent.error && recent.items.length === 0" severity="error" role="alert" :closable="false">
          <div class="notif-panel__error">
            <span>{{ recent.error }}</span>
            <Button
              type="button"
              label="Tentar novamente"
              icon="pi pi-refresh"
              size="small"
              @click="store.fetchRecent()"
            />
          </div>
        </Message>
        <p v-else-if="recent.items.length === 0" class="notif-panel__state" data-testid="notification-empty">
          Você não tem notificações.
        </p>
        <ul v-else class="notif-panel__list" aria-label="Notificações recentes">
          <NotificationItem v-for="item in recent.items" :key="item.id" :notification="item" @navigate="hide()" />
        </ul>
      </div>

      <footer class="notif-panel__footer">
        <RouterLink :to="{ name: 'notifications' }" class="notif-panel__all" @click="hide()">Ver todas</RouterLink>
      </footer>
    </section>
  </div>
</template>

<style scoped>
.notif-bell {
  position: relative;
}

.notif-bell__trigger {
  position: relative;
  display: grid;
  place-items: center;
  width: 2.5rem;
  height: 2.5rem;
  border: 0;
  border-radius: 50%;
  background: none;
  color: var(--gf-text);
  font-size: 1.125rem;
  cursor: pointer;
}

.notif-bell__trigger:hover {
  background: var(--p-highlight-background);
}

.notif-bell__trigger:focus-visible {
  outline: 2px solid var(--p-primary-color);
  outline-offset: 2px;
}

.notif-bell__badge {
  position: absolute;
  top: 0.125rem;
  right: 0;
  min-width: 1.125rem;
  height: 1.125rem;
  padding: 0 0.25rem;
  border-radius: 999px;
  background: var(--p-red-600, #dc2626);
  color: #fff;
  font-size: 0.6875rem;
  font-weight: 700;
  line-height: 1.125rem;
  text-align: center;
}

.notif-panel {
  position: fixed;
  top: calc(var(--gf-topbar-height) + 0.25rem);
  right: 0.5rem;
  left: 0.5rem;
  z-index: 300;
  display: flex;
  flex-direction: column;
  max-height: min(32rem, calc(100vh - var(--gf-topbar-height) - 1rem));
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
  color: var(--gf-text);
  box-shadow: 0 8px 28px rgb(0 0 0 / 0.18);
}

.notif-panel:focus {
  outline: none;
}

.notif-panel__header,
.notif-panel__footer {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 1rem;
}

.notif-panel__header {
  border-bottom: 1px solid var(--gf-border);
}

.notif-panel__footer {
  justify-content: center;
  border-top: 1px solid var(--gf-border);
}

.notif-panel__title {
  margin: 0;
  font-size: 1rem;
  font-weight: 600;
}

.notif-panel__body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
}

.notif-panel__list {
  margin: 0;
  padding: 0;
}

.notif-panel__state {
  margin: 0;
  padding: 2rem 1rem;
  text-align: center;
  color: var(--gf-text-muted);
}

.notif-panel__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.notif-panel__all {
  font-weight: 500;
}

@media (min-width: 640px) {
  .notif-panel {
    position: absolute;
    top: calc(100% + 0.5rem);
    right: 0;
    left: auto;
    width: 26rem;
  }
}
</style>
