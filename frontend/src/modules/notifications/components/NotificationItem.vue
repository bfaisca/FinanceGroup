<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import { formatDateTime } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { invitationStatusLabel, notificationRoute, notificationText, relativeTime } from '../messages'
import { useNotificationsStore } from '../store'
import type { NotificationDto } from '../types'

const props = defineProps<{ notification: NotificationDto }>()
/** Emitido depois de uma navegação (abrir movimentação / entrar no grupo): o painel do sino se fecha. */
const emit = defineEmits<{ navigate: [] }>()

const store = useNotificationsStore()
const router = useRouter()
const toast = useToast()
const busy = ref(false)

const n = computed(() => props.notification)
const text = computed(() => notificationText(n.value))
const target = computed(() => notificationRoute(n.value))
const fullDate = computed(() => formatDateTime(n.value.createdAt))
const relative = computed(() => relativeTime(n.value.createdAt))

const icon = computed(() => {
  switch (n.value.type) {
    case 'GroupTransactionCreated':
      return 'pi pi-plus-circle'
    case 'GroupPaymentRegistered':
      return 'pi pi-wallet'
    default:
      return 'pi pi-envelope'
  }
})

const invitation = computed(() => n.value.invitation)
const canRespond = computed(() => invitation.value?.canRespond === true)
const statusLabel = computed(() => (invitation.value ? invitationStatusLabel(invitation.value.status) : null))
/** Convite pendente que o usuário não pode responder (e-mail diferente, já é membro…). */
const unavailable = computed(() => invitation.value !== null && !canRespond.value && statusLabel.value === null)

async function open(): Promise<void> {
  const to = target.value
  if (!to) return
  void store.markRead(n.value)
  await router.push(to)
  emit('navigate')
}

async function accept(): Promise<void> {
  busy.value = true
  try {
    const group = await store.acceptInvitation(n.value)
    toast.add({
      severity: 'success',
      summary: 'Convite aceito',
      detail: `Você agora participa de “${group.name}”.`,
      life: 4500,
    })
    await router.push({ name: 'group-detail', params: { id: group.id } })
    emit('navigate')
  } catch (e) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível aceitar o convite',
      detail: toAppError(e).message,
      life: 6000,
    })
  } finally {
    busy.value = false
  }
}

async function decline(): Promise<void> {
  busy.value = true
  try {
    await store.declineInvitation(n.value)
    toast.add({ severity: 'success', summary: 'Convite recusado', life: 3500 })
  } catch (e) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível recusar o convite',
      detail: toAppError(e).message,
      life: 6000,
    })
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <li class="notif-item" :class="{ 'notif-item--unread': !n.isRead }" :data-testid="`notification-${n.id}`">
    <span class="notif-item__icon" aria-hidden="true"><i :class="icon" /></span>
    <div class="notif-item__content">
      <component
        :is="target ? 'button' : 'div'"
        class="notif-item__main"
        :class="{ 'notif-item__main--link': target }"
        v-bind="target ? { type: 'button' } : {}"
        @click="target ? open() : undefined"
      >
        <span class="notif-item__text"> <span v-if="!n.isRead" class="sr-only">Não lida. </span>{{ text }} </span>
        <span class="notif-item__meta">
          <span v-if="!n.isRead" class="notif-item__new" data-testid="unread-marker">
            <span class="notif-item__dot" aria-hidden="true" />Nova
          </span>
          <time :datetime="n.createdAt" :title="fullDate">{{ relative }}</time>
        </span>
      </component>

      <div v-if="invitation" class="notif-item__invite">
        <template v-if="canRespond">
          <Button
            type="button"
            label="Aceitar"
            icon="pi pi-check"
            size="small"
            :loading="busy"
            :aria-label="`Aceitar convite para o grupo ${n.group.name}`"
            @click="accept"
          />
          <Button
            type="button"
            label="Recusar"
            icon="pi pi-times"
            size="small"
            severity="secondary"
            outlined
            :disabled="busy"
            :aria-label="`Recusar convite para o grupo ${n.group.name}`"
            @click="decline"
          />
        </template>
        <span v-else-if="statusLabel" class="notif-item__status" data-testid="invitation-status">{{
          statusLabel
        }}</span>
        <span v-else-if="unavailable" class="notif-item__status" data-testid="invitation-status"
          >Convite indisponível</span
        >
        <Button
          v-if="!n.isRead && !canRespond"
          type="button"
          label="Marcar como lida"
          size="small"
          severity="secondary"
          text
          @click="store.markRead(n)"
        />
      </div>
    </div>
  </li>
</template>

<style scoped>
.notif-item {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--gf-border);
  list-style: none;
}

.notif-item--unread {
  background: var(--p-highlight-background);
}

.notif-item__icon {
  display: grid;
  flex: none;
  place-items: center;
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  background: var(--gf-surface);
  border: 1px solid var(--gf-border);
  color: var(--gf-text-muted);
}

.notif-item__content {
  flex: 1;
  min-width: 0;
}

.notif-item__main {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  width: 100%;
  padding: 0;
  border: 0;
  background: none;
  color: var(--gf-text);
  font: inherit;
  text-align: left;
}

.notif-item__main--link {
  cursor: pointer;
  border-radius: 0.25rem;
}

.notif-item__main--link:hover .notif-item__text {
  text-decoration: underline;
}

.notif-item__main--link:focus-visible {
  outline: 2px solid var(--p-primary-color);
  outline-offset: 2px;
}

.notif-item__text {
  overflow-wrap: anywhere;
  line-height: 1.4;
}

.notif-item--unread .notif-item__text {
  font-weight: 600;
}

.notif-item__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.notif-item__new {
  display: inline-flex;
  gap: 0.25rem;
  align-items: center;
  font-weight: 600;
  color: var(--p-primary-color);
}

.notif-item__dot {
  width: 0.5rem;
  height: 0.5rem;
  border-radius: 50%;
  background: var(--p-primary-color);
}

.notif-item__invite {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  margin-top: 0.5rem;
}

.notif-item__status {
  font-size: 0.875rem;
  font-weight: 500;
  color: var(--gf-text-muted);
}
</style>
