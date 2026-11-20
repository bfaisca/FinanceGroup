<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import FormField from '@/components/FormField.vue'
import LoadingState from '@/components/LoadingState.vue'
import ToneChip from '@/components/ToneChip.vue'
import type { GroupInvitationStatus } from '@/types/api'
import { formatDate, formatDateTime } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { useResource } from '../composables'
import { INVITATION_STATUS_PRESENTATION, INVITATION_STATUSES } from '../labels'
import { groupService } from '../services/groupService'
import type { GroupDetailDto, GroupInvitationDto } from '../types'
import { INVITE_CONFLICT_MESSAGE } from '../validation'
import InviteForm from './InviteForm.vue'

const props = defineProps<{ group: GroupDetailDto }>()

const toast = useToast()
const statusFilter = ref<GroupInvitationStatus | null>(null)
const list = useResource(
  () => groupService.listInvitations(props.group.id, statusFilter.value ?? undefined),
  () => `${props.group.id}|${statusFilter.value ?? ''}`,
)

const invitations = computed(() => list.data.value ?? [])
const statusOptions = [
  { value: null as GroupInvitationStatus | null, label: 'Todos os status' },
  ...INVITATION_STATUSES.map((value) => ({
    value: value as GroupInvitationStatus | null,
    label: INVITATION_STATUS_PRESENTATION[value].label,
  })),
]

const inviteForm = ref<InstanceType<typeof InviteForm> | null>(null)
const inviting = ref(false)
const inviteError = ref<string | null>(null)
/** Aviso persistente: o convite existe, mas o e-mail não saiu (§16.1 `emailSent=false`). */
const emailWarning = ref<string | null>(null)

function noteEmailResult(invitation: GroupInvitationDto): void {
  if (invitation.emailSent === false) {
    emailWarning.value = `O convite para ${invitation.email} foi registrado, mas o e-mail não pôde ser enviado. Use “Reenviar” para tentar novamente.`
  } else {
    emailWarning.value = null
  }
}

async function invite(email: string): Promise<void> {
  inviting.value = true
  inviteError.value = null
  try {
    const created = await groupService.invite(props.group.id, email)
    noteEmailResult(created)
    if (created.emailSent !== false) {
      toast.add({ severity: 'success', summary: 'Convite enviado', detail: created.email, life: 3500 })
    }
    inviteForm.value?.reset()
    void list.reload()
  } catch (e) {
    const appError = toAppError(e)
    inviteError.value =
      appError.code === 'conflict' ? INVITE_CONFLICT_MESSAGE : (appError.fieldErrors.email?.[0] ?? appError.message)
  } finally {
    inviting.value = false
  }
}

const busyId = ref<string | null>(null)

async function resend(invitation: GroupInvitationDto): Promise<void> {
  busyId.value = invitation.id
  try {
    const updated = await groupService.resendInvitation(props.group.id, invitation.id)
    noteEmailResult(updated)
    if (updated.emailSent !== false) {
      toast.add({ severity: 'success', summary: 'Convite reenviado', detail: updated.email, life: 3500 })
    }
    void list.reload()
  } catch (e) {
    toast.add({ severity: 'error', summary: 'Não foi possível reenviar', detail: toAppError(e).message, life: 6000 })
  } finally {
    busyId.value = null
  }
}

const revoking = ref<GroupInvitationDto | null>(null)
const confirmVisible = ref(false)

function askRevoke(invitation: GroupInvitationDto): void {
  revoking.value = invitation
  confirmVisible.value = true
}

async function confirmRevoke(): Promise<void> {
  const invitation = revoking.value
  if (!invitation) return
  busyId.value = invitation.id
  try {
    await groupService.revokeInvitation(props.group.id, invitation.id)
    toast.add({ severity: 'success', summary: 'Convite revogado', detail: invitation.email, life: 3500 })
  } catch (e) {
    toast.add({ severity: 'error', summary: 'Não foi possível revogar', detail: toAppError(e).message, life: 6000 })
  } finally {
    confirmVisible.value = false
    busyId.value = null
    void list.reload()
  }
}

/** Reenviar gera novo token e validade: faz sentido para convites pendentes ou expirados. */
function canResend(invitation: GroupInvitationDto): boolean {
  return invitation.status === 'Pending' || invitation.status === 'Expired'
}
</script>

<template>
  <div class="stack">
    <section class="surface-card" aria-labelledby="invite-title">
      <h2 id="invite-title" class="group-invites__title">Convidar por e-mail</h2>
      <InviteForm ref="inviteForm" :loading="inviting" :error-message="inviteError" @submit="invite" />
      <Message v-if="emailWarning" severity="warn" class="group-invites__block" data-testid="email-warning">
        {{ emailWarning }}
      </Message>
    </section>

    <section aria-labelledby="invites-list-title">
      <div class="group-invites__header">
        <h2 id="invites-list-title" class="group-invites__title">Convites</h2>
        <FormField id="invite-status-filter" v-slot="{ id }" label="Filtrar por status" class="group-invites__filter">
          <Select
            v-model="statusFilter"
            :input-id="id"
            :options="statusOptions"
            option-label="label"
            option-value="value"
            placeholder="Todos os status"
            fluid
          />
        </FormField>
      </div>

      <LoadingState v-if="list.loading.value && !list.loadedOnce.value" label="Carregando convites…" />

      <Message v-else-if="list.error.value && !list.data.value" severity="error" role="alert">
        <div class="group-invites__error">
          <span>{{ list.error.value }}</span>
          <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="list.reload()" />
        </div>
      </Message>

      <EmptyState
        v-else-if="invitations.length === 0"
        icon="pi pi-envelope"
        :title="statusFilter ? 'Nenhum convite com este status' : 'Nenhum convite enviado'"
        description="Os convites enviados por e-mail aparecem aqui, com o status de cada um."
      />

      <ul v-else class="group-invites__list" aria-label="Convites do grupo" :aria-busy="list.loading.value">
        <li v-for="invitation in invitations" :key="invitation.id" class="group-invites__item">
          <div class="group-invites__info">
            <span class="group-invites__email">{{ invitation.email }}</span>
            <span class="group-invites__meta">
              <ToneChip
                :label="INVITATION_STATUS_PRESENTATION[invitation.status].label"
                :icon="INVITATION_STATUS_PRESENTATION[invitation.status].icon"
                :tone="INVITATION_STATUS_PRESENTATION[invitation.status].tone"
              />
              <span>Por {{ invitation.invitedByName }} em {{ formatDateTime(invitation.createdAt) }}</span>
              <span>
                {{ invitation.status === 'Pending' ? 'Válido até' : 'Validade' }}
                {{ formatDate(invitation.expiresAt.slice(0, 10)) }}
              </span>
            </span>
          </div>
          <div class="group-invites__actions">
            <Button
              v-if="canResend(invitation)"
              type="button"
              label="Reenviar"
              icon="pi pi-refresh"
              size="small"
              severity="secondary"
              outlined
              :aria-label="`Reenviar convite para ${invitation.email}`"
              :disabled="busyId !== null"
              @click="resend(invitation)"
            />
            <Button
              v-if="invitation.status === 'Pending'"
              type="button"
              label="Revogar"
              icon="pi pi-ban"
              size="small"
              severity="danger"
              outlined
              :aria-label="`Revogar convite para ${invitation.email}`"
              :disabled="busyId !== null"
              @click="askRevoke(invitation)"
            />
          </div>
        </li>
      </ul>
    </section>

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      header="Revogar convite"
      :message="`Revogar o convite enviado para ${revoking?.email ?? ''}? O link deixará de funcionar.`"
      confirm-label="Revogar convite"
      severity="danger"
      :loading="busyId !== null"
      @confirm="confirmRevoke"
    />
  </div>
</template>

<style scoped>
.group-invites__title {
  font-size: 1.0625rem;
  font-weight: 600;
  margin-bottom: 0.75rem;
}

.group-invites__block {
  margin: 0.75rem 0;
}

.group-invites__header {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: flex-end;
  justify-content: space-between;
  margin-bottom: 0.75rem;
}

.group-invites__header .group-invites__title {
  margin-bottom: 0;
}

.group-invites__filter {
  min-width: min(100%, 14rem);
}

.group-invites__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.group-invites__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.group-invites__item {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem 1rem;
  align-items: center;
  justify-content: space-between;
  padding: 0.875rem 1rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
  background: var(--gf-surface);
}

.group-invites__info {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  min-width: 0;
}

.group-invites__email {
  font-weight: 600;
  overflow-wrap: anywhere;
}

.group-invites__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem 0.75rem;
  align-items: center;
  font-size: 0.875rem;
  color: var(--gf-text-muted);
}

.group-invites__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
