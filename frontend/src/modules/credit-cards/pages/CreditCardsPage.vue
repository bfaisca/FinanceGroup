<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useAccountsStore } from '@/stores/accounts'
import { useCreditCardsStore } from '@/stores/creditCards'
import type { CreditCardDto } from '@/types/api'
import { toAppError } from '@/utils/problem'
import CreditCardFormDialog from '../components/CreditCardFormDialog.vue'
import CreditCardItem from '../components/CreditCardItem.vue'

const cards = useCreditCardsStore()
const accounts = useAccountsStore()
const router = useRouter()
const toast = useToast()

const showInactive = ref(false)
const formVisible = ref(false)
const editing = ref<CreditCardDto | null>(null)
const confirmVisible = ref(false)
const toggling = ref<CreditCardDto | null>(null)
const busy = ref(false)

const visibleCards = computed(() => cards.items.filter((c) => showInactive.value || c.isActive))
const hasInactive = computed(() => cards.items.some((c) => !c.isActive))

onMounted(() => {
  // Valores de limite/fatura mudam com as movimentações: sempre recarrega ao entrar.
  void cards.load(true)
  void accounts.load()
})

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(card: CreditCardDto): void {
  editing.value = card
  formVisible.value = true
}

function viewPurchases(card: CreditCardDto): void {
  void router.push({ name: 'transactions', query: { creditCardId: card.id } })
}

async function onToggle(card: CreditCardDto): Promise<void> {
  if (card.isActive) {
    toggling.value = card
    confirmVisible.value = true
    return
  }
  await runToggle(card)
}

async function runToggle(card: CreditCardDto): Promise<void> {
  busy.value = true
  try {
    if (card.isActive) await cards.deactivate(card.id)
    else await cards.activate(card.id)
    toast.add({ severity: 'success', summary: card.isActive ? 'Cartão desativado' : 'Cartão reativado', life: 3500 })
  } catch (error) {
    toast.add({
      severity: 'error',
      summary: 'Não foi possível concluir',
      detail: toAppError(error).message,
      life: 6000,
    })
  } finally {
    confirmVisible.value = false
    busy.value = false
  }
}
</script>

<template>
  <div>
    <PageHeader title="Cartões" description="Seus cartões de crédito, limites e datas de fechamento e vencimento.">
      <template #actions>
        <Button type="button" label="Novo cartão" icon="pi pi-plus" @click="openCreate" />
      </template>
    </PageHeader>

    <div v-if="hasInactive" class="cards-page__filter">
      <Checkbox v-model="showInactive" input-id="show-inactive-cards" binary />
      <label for="show-inactive-cards">Mostrar cartões inativos</label>
    </div>

    <LoadingState v-if="cards.loading && !cards.loaded" label="Carregando cartões…" />

    <Message v-else-if="cards.error && !cards.loaded" severity="error" role="alert">
      <div class="cards-page__error">
        <span>{{ cards.error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="cards.load(true)" />
      </div>
    </Message>

    <EmptyState
      v-else-if="visibleCards.length === 0"
      icon="pi pi-credit-card"
      :title="cards.items.length === 0 ? 'Nenhum cartão cadastrado' : 'Nenhum cartão ativo'"
      :description="
        cards.items.length === 0
          ? 'Cadastre seu primeiro cartão para registrar compras, acompanhar o limite e ver a fatura aberta.'
          : 'Todos os seus cartões estão inativos. Reative um deles ou cadastre um novo cartão.'
      "
    >
      <Button
        type="button"
        :label="cards.items.length === 0 ? 'Cadastrar primeiro cartão' : 'Novo cartão'"
        icon="pi pi-plus"
        @click="openCreate"
      />
    </EmptyState>

    <ul v-else class="cards-page__grid" aria-label="Cartões de crédito">
      <li v-for="card in visibleCards" :key="card.id">
        <CreditCardItem :card="card" :busy="busy" @edit="openEdit" @toggle="onToggle" @purchases="viewPurchases" />
      </li>
    </ul>

    <CreditCardFormDialog v-model:visible="formVisible" :card="editing" />

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      header="Desativar cartão"
      :message="`Desativar “${toggling?.name ?? ''}”? O cartão deixa de aceitar novas compras, parcelamentos e recorrências. O histórico é preservado e você pode reativá-lo depois.`"
      confirm-label="Desativar"
      severity="danger"
      :loading="busy"
      @confirm="toggling && runToggle(toggling)"
    />
  </div>
</template>

<style scoped>
.cards-page__filter {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-bottom: 1rem;
}

.cards-page__grid {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(min(100%, 22rem), 1fr));
  gap: 1rem;
}

.cards-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
