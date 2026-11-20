<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import ToneChip from '@/components/ToneChip.vue'
import { toAppError } from '@/utils/problem'
import GroupDashboardTab from '../components/GroupDashboardTab.vue'
import GroupInvitationsTab from '../components/GroupInvitationsTab.vue'
import GroupMembersTab from '../components/GroupMembersTab.vue'
import GroupRoleChip from '../components/GroupRoleChip.vue'
import GroupSettingsTab from '../components/GroupSettingsTab.vue'
import GroupTransactionsTab from '../components/GroupTransactionsTab.vue'
import { useResource } from '../composables'
import { groupService } from '../services/groupService'
import type { GroupDetailDto } from '../types'

type GroupTab = 'summary' | 'transactions' | 'members' | 'invitations' | 'settings'

const route = useRoute()
const router = useRouter()
const toast = useToast()

const groupId = computed(() => String(route.params.id))
const detail = useResource(
  () => groupService.get(groupId.value),
  () => groupId.value,
  { clearOnKeyChange: true },
)
const group = detail.data

/** Abas visíveis: Convites só para quem pode convidar (`permissions.canInvite`). */
const tabs = computed(() => {
  const list: { value: GroupTab; label: string }[] = [
    { value: 'summary', label: 'Resumo' },
    { value: 'transactions', label: 'Movimentações' },
    { value: 'members', label: 'Membros' },
  ]
  if (group.value?.permissions.canInvite) list.push({ value: 'invitations', label: 'Convites' })
  list.push({ value: 'settings', label: 'Configurações' })
  return list
})

const tab = computed<GroupTab>(() => {
  const value = Array.isArray(route.query.tab) ? route.query.tab[0] : route.query.tab
  return tabs.value.find((t) => t.value === value)?.value ?? 'summary'
})

/** Trocar de aba descarta os parâmetros da aba anterior (filtros, detalhe aberto, período). */
function setTab(value: string | number): void {
  if (value === tab.value) return
  void router.replace({ query: value === 'summary' ? {} : { tab: String(value) } })
}

function applyUpdated(updated: GroupDetailDto): void {
  group.value = updated
}

// ---- Sair do grupo (acionado pelas abas Membros e Configurações) ----
const leaveVisible = ref(false)
const leaving = ref(false)

async function confirmLeave(): Promise<void> {
  if (!group.value) return
  leaving.value = true
  try {
    await groupService.leave(group.value.id)
    toast.add({ severity: 'success', summary: 'Você saiu do grupo', detail: group.value.name, life: 4000 })
    leaveVisible.value = false
    await router.replace({ name: 'groups' })
  } catch (e) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível sair do grupo',
      detail: toAppError(e).message,
      life: 6000,
    })
    leaveVisible.value = false
    void detail.reload()
  } finally {
    leaving.value = false
  }
}

const notFound = computed(() => detail.errorStatus.value === 404)
</script>

<template>
  <div>
    <LoadingState v-if="detail.loading.value && !group" label="Carregando grupo…" />

    <template v-else-if="detail.error.value && !group">
      <PageHeader title="Grupo" />
      <Message v-if="notFound" severity="warn" :closable="false" data-testid="group-not-found">
        <div class="group-page__error">
          <span>Grupo não encontrado, ou você não participa mais dele.</span>
          <Button
            type="button"
            label="Voltar aos grupos"
            icon="pi pi-arrow-left"
            size="small"
            @click="router.push({ name: 'groups' })"
          />
        </div>
      </Message>
      <Message v-else severity="error" role="alert">
        <div class="group-page__error">
          <span>{{ detail.error.value }}</span>
          <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="detail.reload()" />
        </div>
      </Message>
    </template>

    <template v-else-if="group">
      <RouterLink :to="{ name: 'groups' }" class="group-page__back"
        ><i class="pi pi-arrow-left" aria-hidden="true" /> Todos os grupos</RouterLink
      >

      <PageHeader :title="group.name" :description="group.description ?? undefined">
        <template #actions>
          <GroupRoleChip :role="group.myRole" />
          <ToneChip v-if="!group.isActive" label="Arquivado" icon="pi pi-inbox" tone="neutral" />
          <span class="group-page__count">
            <i class="pi pi-users" aria-hidden="true" />
            {{ group.memberCount }} {{ group.memberCount === 1 ? 'membro' : 'membros' }}
          </span>
        </template>
      </PageHeader>

      <Message
        v-if="!group.isActive"
        :pt="NOTE_PT"
        severity="info"
        :closable="false"
        class="group-page__archived"
        data-testid="archived-banner"
      >
        Este grupo está arquivado e é somente leitura: não é possível cadastrar movimentações, editar, cancelar ou
        registrar pagamentos.
      </Message>

      <Message v-if="detail.error.value" severity="error" role="alert" class="group-page__archived">
        <div class="group-page__error">
          <span>Não foi possível atualizar: {{ detail.error.value }}</span>
          <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="detail.reload()" />
        </div>
      </Message>

      <Tabs :value="tab" scrollable @update:value="setTab">
        <TabList>
          <Tab v-for="t in tabs" :key="t.value" :value="t.value">{{ t.label }}</Tab>
        </TabList>
        <TabPanels>
          <TabPanel v-for="t in tabs" :key="t.value" :value="t.value">
            <template v-if="tab === t.value">
              <GroupDashboardTab v-if="t.value === 'summary'" :group="group" />
              <GroupTransactionsTab v-else-if="t.value === 'transactions'" :group="group" />
              <GroupMembersTab
                v-else-if="t.value === 'members'"
                :group="group"
                @changed="detail.reload()"
                @leave="leaveVisible = true"
              />
              <GroupInvitationsTab v-else-if="t.value === 'invitations'" :group="group" />
              <GroupSettingsTab v-else :group="group" @updated="applyUpdated" @leave="leaveVisible = true" />
            </template>
          </TabPanel>
        </TabPanels>
      </Tabs>

      <AppConfirmDialog
        v-model:visible="leaveVisible"
        header="Sair do grupo"
        :message="`Sair de “${group.name}”? Você deixará de ver o grupo e só volta se for convidado novamente. Suas movimentações e pagamentos continuam no histórico.`"
        confirm-label="Sair do grupo"
        severity="danger"
        :loading="leaving"
        @confirm="confirmLeave"
      />
    </template>
  </div>
</template>

<style scoped>
.group-page__back {
  display: inline-flex;
  gap: 0.375rem;
  align-items: center;
  margin-bottom: 0.75rem;
  font-size: 0.9375rem;
}

.group-page__count {
  display: inline-flex;
  gap: 0.375rem;
  align-items: center;
  color: var(--gf-text-muted);
}

.group-page__archived {
  margin-bottom: 1rem;
}

.group-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
