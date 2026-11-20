<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { useResource } from '../composables'
import { invitationState, type InvitationState } from '../invitation'
import { invitationService } from '../services/invitationService'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const auth = useAuthStore()

const token = computed(() => String(route.params.token))
const preview = useResource(
  () => invitationService.preview(token.value),
  () => token.value,
  {
    clearOnKeyChange: true,
  },
)
const data = preview.data
const state = computed<InvitationState | null>(() => (data.value ? invitationState(data.value) : null))
const notFound = computed(() => preview.errorStatus.value === 404)

const STATE_COPY: Record<
  Exclude<InvitationState, 'actionable' | 'wrongEmail'>,
  { text: string; severity: 'warn' | 'info' }
> = {
  alreadyMember: { text: 'Você já participa deste grupo.', severity: 'info' },
  expired: {
    text: 'Este convite expirou. Peça a um administrador do grupo que envie um novo convite.',
    severity: 'warn',
  },
  revoked: { text: 'Este convite foi revogado e não pode mais ser usado.', severity: 'warn' },
  accepted: { text: 'Este convite já foi aceito.', severity: 'info' },
  declined: { text: 'Este convite foi recusado.', severity: 'info' },
}

const actionError = ref<string | null>(null)
const busy = ref(false)

async function accept(): Promise<void> {
  busy.value = true
  actionError.value = null
  try {
    const group = await invitationService.accept(token.value)
    toast.add({
      severity: 'success',
      summary: 'Convite aceito',
      detail: `Você agora participa de “${group.name}”.`,
      life: 4500,
    })
    await router.replace({ name: 'group-detail', params: { id: group.id } })
  } catch (e) {
    // 403 (e-mail diferente) e 422 (expirado/revogado/já usado/já membro): mostra a explicação e atualiza o estado.
    actionError.value = toAppError(e).message
    void preview.reload()
  } finally {
    busy.value = false
  }
}

const declineVisible = ref(false)

async function decline(): Promise<void> {
  busy.value = true
  actionError.value = null
  try {
    await invitationService.decline(token.value)
    declineVisible.value = false
    toast.add({ severity: 'success', summary: 'Convite recusado', life: 3500 })
    await router.replace({ name: 'groups' })
  } catch (e) {
    declineVisible.value = false
    actionError.value = toAppError(e).message
    void preview.reload()
  } finally {
    busy.value = false
  }
}

/** Encerra a sessão atual e volta ao login mantendo o link do convite como destino. */
async function switchAccount(): Promise<void> {
  await auth.logout()
  await router.replace({ name: 'login', query: { redirect: route.fullPath } })
}
</script>

<template>
  <div class="invitation">
    <PageHeader
      title="Convite para grupo"
      description="Participe de um grupo para compartilhar movimentações com transparência."
    />

    <LoadingState v-if="preview.loading.value && !data" label="Carregando convite…" />

    <Message v-else-if="notFound" severity="warn" :closable="false" data-testid="invitation-not-found">
      Convite não encontrado. O link pode estar incompleto ou inválido: confira o e-mail recebido ou peça um novo
      convite.
    </Message>

    <Message v-else-if="preview.error.value && !data" severity="error" role="alert">
      <div class="invitation__row">
        <span>{{ preview.error.value }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="preview.reload()" />
      </div>
    </Message>

    <section v-else-if="data && state" class="surface-card invitation__card" aria-labelledby="invitation-group">
      <dl class="invitation__facts">
        <div>
          <dt>Grupo</dt>
          <dd id="invitation-group" class="invitation__group" data-testid="invitation-group">{{ data.groupName }}</dd>
        </div>
        <div>
          <dt>Convidado por</dt>
          <dd data-testid="invitation-by">{{ data.invitedByName }}</dd>
        </div>
        <div>
          <dt>E-mail do convite</dt>
          <dd data-testid="invitation-email">{{ data.email }}</dd>
        </div>
        <div>
          <dt>Válido até</dt>
          <dd>{{ formatDateTime(data.expiresAt) }}</dd>
        </div>
      </dl>

      <Message v-if="actionError" severity="error" role="alert" data-testid="invitation-error">{{
        actionError
      }}</Message>

      <template v-if="state === 'actionable'">
        <p class="invitation__text">
          Ao aceitar, você poderá ver as movimentações compartilhadas do grupo e registrar os seus próprios pagamentos.
          Suas contas, cartões e movimentações individuais continuam privados.
        </p>
        <div class="invitation__actions">
          <Button type="button" label="Aceitar convite" icon="pi pi-check" :loading="busy" @click="accept" />
          <Button
            type="button"
            label="Recusar"
            icon="pi pi-times"
            severity="secondary"
            outlined
            :disabled="busy"
            @click="declineVisible = true"
          />
        </div>
      </template>

      <template v-else-if="state === 'wrongEmail'">
        <Message severity="warn" :closable="false" data-testid="invitation-wrong-email">
          Este convite foi enviado para <strong>{{ data.email }}</strong>, mas você está conectado como
          <strong>{{ auth.user?.email ?? 'outra conta' }}</strong>. Entre ou crie uma conta com o e-mail do convite para
          aceitá-lo.
        </Message>
        <div class="invitation__actions">
          <Button type="button" label="Entrar com outra conta" icon="pi pi-sign-out" @click="switchAccount" />
          <Button
            type="button"
            label="Ir para meus grupos"
            severity="secondary"
            outlined
            @click="router.push({ name: 'groups' })"
          />
        </div>
      </template>

      <template v-else>
        <Message :severity="STATE_COPY[state].severity" :closable="false" data-testid="invitation-state">
          {{ STATE_COPY[state].text }}
        </Message>
        <div class="invitation__actions">
          <Button
            type="button"
            label="Ir para meus grupos"
            icon="pi pi-users"
            @click="router.push({ name: 'groups' })"
          />
        </div>
      </template>
    </section>

    <AppConfirmDialog
      v-model:visible="declineVisible"
      header="Recusar convite"
      message="Recusar o convite? O link deixará de funcionar e será necessário um novo convite para participar."
      confirm-label="Recusar convite"
      severity="danger"
      :loading="busy"
      @confirm="decline"
    />
  </div>
</template>

<style scoped>
.invitation__card {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  max-width: 40rem;
}

.invitation__facts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 12rem), 1fr));
  gap: 0.75rem 1rem;
  margin: 0;
}

.invitation__facts dt {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.invitation__facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.invitation__group {
  font-size: 1.125rem;
  font-weight: 600;
}

.invitation__text {
  margin: 0;
  color: var(--gf-text-muted);
}

.invitation__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.invitation__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
