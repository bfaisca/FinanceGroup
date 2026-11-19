<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useAccountsStore } from '@/stores/accounts'
import type { AccountDto } from '@/types/api'
import { toAppError } from '@/utils/problem'
import AccountCard from '../components/AccountCard.vue'
import AccountFormDialog from '../components/AccountFormDialog.vue'

const accounts = useAccountsStore()
const toast = useToast()

const showInactive = ref(false)
const formVisible = ref(false)
const editing = ref<AccountDto | null>(null)
const confirmVisible = ref(false)
const toggling = ref<AccountDto | null>(null)
const busy = ref(false)

const visibleAccounts = computed(() => accounts.items.filter((a) => showInactive.value || a.isActive))
const hasInactive = computed(() => accounts.items.some((a) => !a.isActive))

onMounted(() => {
  void accounts.load(true)
})

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(account: AccountDto): void {
  editing.value = account
  formVisible.value = true
}

async function onToggle(account: AccountDto): Promise<void> {
  if (account.isActive) {
    toggling.value = account
    confirmVisible.value = true
    return
  }
  // Reativar não é destrutivo: dispensa confirmação.
  await runToggle(account)
}

async function runToggle(account: AccountDto): Promise<void> {
  busy.value = true
  try {
    if (account.isActive) await accounts.deactivate(account.id)
    else await accounts.activate(account.id)
    toast.add({
      severity: 'success',
      summary: account.isActive ? 'Conta desativada' : 'Conta reativada',
      life: 3500,
    })
    confirmVisible.value = false
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível concluir',
      detail: toAppError(error).message,
      life: 6000,
    })
    confirmVisible.value = false
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div>
    <PageHeader title="Contas" description="Suas contas, os saldos que você informou e o saldo projetado.">
      <template #actions>
        <Button type="button" label="Nova conta" icon="pi pi-plus" @click="openCreate" />
      </template>
    </PageHeader>

    <div v-if="hasInactive" class="accounts-page__filter">
      <Checkbox v-model="showInactive" input-id="show-inactive" binary />
      <label for="show-inactive">Mostrar contas inativas</label>
    </div>

    <LoadingState v-if="accounts.loading && !accounts.loaded" label="Carregando contas…" />

    <Message v-else-if="accounts.error && !accounts.loaded" severity="error" role="alert">
      <div class="accounts-page__error">
        <span>{{ accounts.error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="accounts.load(true)" />
      </div>
    </Message>

    <EmptyState
      v-else-if="visibleAccounts.length === 0"
      icon="pi pi-building-columns"
      :title="accounts.items.length === 0 ? 'Nenhuma conta cadastrada' : 'Nenhuma conta ativa'"
      :description="
        accounts.items.length === 0
          ? 'Cadastre uma conta e informe seu saldo para acompanhar o saldo projetado.'
          : 'Todas as suas contas estão inativas. Reative uma delas ou crie uma nova conta.'
      "
    >
      <Button type="button" label="Nova conta" icon="pi pi-plus" @click="openCreate" />
    </EmptyState>

    <ul v-else class="accounts-page__grid" aria-label="Contas">
      <li v-for="account in visibleAccounts" :key="account.id">
        <AccountCard :account="account" :busy="busy" @edit="openEdit" @toggle="onToggle" />
      </li>
    </ul>

    <AccountFormDialog v-model:visible="formVisible" :account="editing" />

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      header="Desativar conta"
      :message="`Desativar “${toggling?.name ?? ''}”? A conta deixa de aceitar novas movimentações e saldos e sai dos totais do dashboard. O histórico é preservado e você pode reativá-la depois.`"
      confirm-label="Desativar"
      severity="danger"
      :loading="busy"
      @confirm="toggling && runToggle(toggling)"
    />
  </div>
</template>

<style scoped>
.accounts-page__filter {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-bottom: 1rem;
}

.accounts-page__grid {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 22rem), 1fr));
  gap: 1rem;
}

.accounts-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
