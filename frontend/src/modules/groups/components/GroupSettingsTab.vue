<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, ref } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import { toAppError } from '@/utils/problem'
import { groupService } from '../services/groupService'
import type { GroupDetailDto, GroupRequest } from '../types'
import { GROUP_FORM_FIELDS } from '../validation'
import GroupForm from './GroupForm.vue'

const props = defineProps<{ group: GroupDetailDto }>()
const emit = defineEmits<{ updated: [group: GroupDetailDto]; leave: [] }>()

const toast = useToast()
const permissions = computed(() => props.group.permissions)

// ---- Editar dados ----
/** Descarta alterações não salvas ao cancelar. */
const formKey = ref(0)
const saving = ref(false)
const saveError = ref<string | null>(null)
const saveFieldErrors = ref<Record<string, string[]>>({})

async function save(request: GroupRequest): Promise<void> {
  saving.value = true
  saveError.value = null
  saveFieldErrors.value = {}
  try {
    const updated = await groupService.update(props.group.id, request)
    toast.add({ severity: 'success', summary: 'Grupo atualizado', life: 3500 })
    emit('updated', updated)
  } catch (e) {
    const appError = toAppError(e)
    saveFieldErrors.value = appError.fieldErrors
    const mapped = (GROUP_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    saveError.value = mapped ? null : appError.message
  } finally {
    saving.value = false
  }
}

// ---- Arquivar / reativar ----
const archiveConfirmVisible = ref(false)
const archiving = ref(false)

async function toggleArchive(): Promise<void> {
  archiving.value = true
  try {
    const updated = props.group.isActive
      ? await groupService.archive(props.group.id)
      : await groupService.unarchive(props.group.id)
    toast.add({
      severity: 'success',
      summary: updated.isActive ? 'Grupo reativado' : 'Grupo arquivado',
      life: 3500,
    })
    emit('updated', updated)
  } catch (e) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível concluir a ação',
      detail: toAppError(e).message,
      life: 6000,
    })
  } finally {
    archiving.value = false
    archiveConfirmVisible.value = false
  }
}

function onArchiveClick(): void {
  if (props.group.isActive) archiveConfirmVisible.value = true
  else void toggleArchive()
}
</script>

<template>
  <div class="stack">
    <section class="surface-card" aria-labelledby="settings-data-title">
      <h2 id="settings-data-title" class="group-settings__title">Dados do grupo</h2>
      <GroupForm
        v-if="permissions.canEditGroup"
        :key="`${group.updatedAt}-${formKey}`"
        id-prefix="group-settings"
        :initial="group"
        :loading="saving"
        :error-message="saveError"
        :field-errors="saveFieldErrors"
        submit-label="Salvar alterações"
        @submit="save"
        @cancel="formKey += 1"
      />
      <dl v-else class="group-settings__facts">
        <div>
          <dt>Nome</dt>
          <dd>{{ group.name }}</dd>
        </div>
        <div>
          <dt>Descrição</dt>
          <dd>{{ group.description || '—' }}</dd>
        </div>
        <div class="group-settings__hint">Somente administradores podem editar os dados do grupo.</div>
      </dl>
    </section>

    <section v-if="permissions.canArchive" class="surface-card" aria-labelledby="settings-archive-title">
      <h2 id="settings-archive-title" class="group-settings__title">
        {{ group.isActive ? 'Arquivar grupo' : 'Reativar grupo' }}
      </h2>
      <p class="group-settings__text">
        {{
          group.isActive
            ? 'Um grupo arquivado fica somente leitura: não aceita novas movimentações, edições, cancelamentos ou pagamentos, e some da lista padrão. Nada é apagado e você pode reativá-lo quando quiser.'
            : 'Este grupo está arquivado (somente leitura). Reative-o para voltar a cadastrar movimentações e registrar pagamentos.'
        }}
      </p>
      <Button
        type="button"
        :label="group.isActive ? 'Arquivar grupo' : 'Reativar grupo'"
        :icon="group.isActive ? 'pi pi-inbox' : 'pi pi-replay'"
        :severity="group.isActive ? 'secondary' : undefined"
        :outlined="group.isActive"
        :loading="archiving && !archiveConfirmVisible"
        @click="onArchiveClick"
      />
    </section>

    <section class="surface-card" aria-labelledby="settings-leave-title">
      <h2 id="settings-leave-title" class="group-settings__title">Sair do grupo</h2>
      <template v-if="permissions.canLeave">
        <p class="group-settings__text">
          Ao sair, você deixa de ver o grupo. Suas movimentações e pagamentos continuam no histórico dos participantes.
          Você só volta se for convidado novamente.
        </p>
        <Button
          type="button"
          label="Sair do grupo"
          icon="pi pi-sign-out"
          severity="danger"
          outlined
          @click="emit('leave')"
        />
      </template>
      <Message v-else :pt="NOTE_PT" severity="info" :closable="false">
        {{
          group.myRole === 'Owner'
            ? 'O proprietário não pode sair do grupo. A transferência de propriedade ainda não está disponível.'
            : 'Você não pode sair deste grupo no momento.'
        }}
      </Message>
    </section>

    <AppConfirmDialog
      v-model:visible="archiveConfirmVisible"
      header="Arquivar grupo"
      message="O grupo ficará somente leitura e sairá da lista padrão. Você poderá reativá-lo depois."
      confirm-label="Arquivar"
      :loading="archiving"
      @confirm="toggleArchive"
    />
  </div>
</template>

<style scoped>
.group-settings__title {
  font-size: 1.0625rem;
  font-weight: 600;
  margin-bottom: 0.75rem;
}

.group-settings__text {
  margin: 0 0 1rem;
  color: var(--gf-text-muted);
}

.group-settings__facts {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin: 0;
}

.group-settings__facts dt {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.group-settings__facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.group-settings__hint {
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}
</style>
