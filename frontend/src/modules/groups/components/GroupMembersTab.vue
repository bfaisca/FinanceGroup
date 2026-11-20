<script setup lang="ts">
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import ToneChip from '@/components/ToneChip.vue'
import { formatDate } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { memberActions } from '../members'
import { groupService } from '../services/groupService'
import type { AssignableRole, GroupDetailDto, GroupMemberDto } from '../types'
import GroupRoleChip from './GroupRoleChip.vue'

const props = defineProps<{ group: GroupDetailDto }>()
const emit = defineEmits<{ changed: []; leave: [] }>()

const toast = useToast()
const busyId = ref<string | null>(null)
const removing = ref<GroupMemberDto | null>(null)
const confirmVisible = ref(false)

const members = computed(() => props.group.members.filter((m) => m.status === 'Active'))
const rows = computed(() =>
  members.value.map((member) => ({
    member,
    actions: memberActions(props.group.myRole, props.group.permissions.canManageMembers, member),
  })),
)

async function changeRole(member: GroupMemberDto, role: AssignableRole): Promise<void> {
  busyId.value = member.id
  try {
    await groupService.changeRole(props.group.id, member.id, role)
    toast.add({
      severity: 'success',
      summary: role === 'Admin' ? 'Promovido a administrador' : 'Rebaixado a membro',
      detail: member.name,
      life: 3500,
    })
    emit('changed')
  } catch (e) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível alterar o papel',
      detail: toAppError(e).message,
      life: 6000,
    })
    emit('changed')
  } finally {
    busyId.value = null
  }
}

function askRemove(member: GroupMemberDto): void {
  removing.value = member
  confirmVisible.value = true
}

async function confirmRemove(): Promise<void> {
  const member = removing.value
  if (!member) return
  busyId.value = member.id
  try {
    await groupService.removeMember(props.group.id, member.id)
    toast.add({ severity: 'success', summary: 'Participante removido', detail: member.name, life: 3500 })
    confirmVisible.value = false
    emit('changed')
  } catch (e) {
    toast.add({ severity: 'error', summary: 'Não foi possível remover', detail: toAppError(e).message, life: 6000 })
    confirmVisible.value = false
    emit('changed')
  } finally {
    busyId.value = null
  }
}
</script>

<template>
  <section aria-labelledby="members-title">
    <h2 id="members-title" class="group-members__title">
      Participantes ativos <span class="group-members__count">({{ members.length }})</span>
    </h2>
    <p class="group-members__note">
      Todos os participantes ativos veem as movimentações compartilhadas do grupo. Quem sai ou é removido deixa de ver o
      grupo, mas o histórico de movimentações e pagamentos é preservado.
    </p>

    <EmptyState v-if="rows.length === 0" icon="pi pi-users" title="Nenhum participante ativo" />

    <ul v-else class="group-members__list" aria-label="Participantes do grupo">
      <li v-for="{ member, actions } in rows" :key="member.id" class="group-members__item">
        <div class="group-members__info">
          <span class="group-members__name">
            {{ member.name }}
            <ToneChip v-if="member.isMe" label="Você" icon="pi pi-user" tone="info" />
          </span>
          <span class="group-members__meta">
            <GroupRoleChip :role="member.role" />
            <span>Desde {{ formatDate(member.joinedAt.slice(0, 10)) }}</span>
          </span>
        </div>
        <div class="group-members__actions">
          <Button
            v-if="actions.canPromote"
            type="button"
            label="Tornar administrador"
            icon="pi pi-arrow-up"
            size="small"
            severity="secondary"
            outlined
            :aria-label="`Tornar ${member.name} administrador`"
            :disabled="busyId !== null"
            @click="changeRole(member, 'Admin')"
          />
          <Button
            v-if="actions.canDemote"
            type="button"
            label="Tornar membro"
            icon="pi pi-arrow-down"
            size="small"
            severity="secondary"
            outlined
            :aria-label="`Rebaixar ${member.name} a membro`"
            :disabled="busyId !== null"
            @click="changeRole(member, 'Member')"
          />
          <Button
            v-if="actions.canRemove"
            type="button"
            label="Remover"
            icon="pi pi-user-minus"
            size="small"
            severity="danger"
            outlined
            :aria-label="`Remover ${member.name} do grupo`"
            :disabled="busyId !== null"
            @click="askRemove(member)"
          />
          <Button
            v-if="member.isMe && group.permissions.canLeave"
            type="button"
            label="Sair do grupo"
            icon="pi pi-sign-out"
            size="small"
            severity="secondary"
            outlined
            @click="emit('leave')"
          />
        </div>
      </li>
    </ul>

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      header="Remover participante"
      :message="`Remover ${removing?.name ?? 'o participante'} do grupo? Ele deixará de ver o grupo, mas o histórico de movimentações e pagamentos é mantido. Ele poderá ser convidado novamente.`"
      confirm-label="Remover"
      severity="danger"
      :loading="busyId !== null"
      @confirm="confirmRemove"
    />
  </section>
</template>

<style scoped>
.group-members__title {
  font-size: 1.0625rem;
  font-weight: 600;
}

.group-members__count {
  color: var(--gf-text-muted);
  font-weight: 400;
}

.group-members__note {
  margin: 0.25rem 0 1rem;
  color: var(--gf-text-muted);
  font-size: 0.9375rem;
}

.group-members__list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.group-members__item {
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

.group-members__info {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  min-width: 0;
}

.group-members__name {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
  font-weight: 600;
  overflow-wrap: anywhere;
}

.group-members__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem 0.75rem;
  align-items: center;
  font-size: 0.875rem;
  color: var(--gf-text-muted);
}

.group-members__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
