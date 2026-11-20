<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useCreditCardsStore } from '@/stores/creditCards'
import type { RecurrenceDto } from '@/types/api'
import { toAppError } from '@/utils/problem'
import RecurrenceFormDialog from '../components/RecurrenceFormDialog.vue'
import RecurrenceList from '../components/RecurrenceList.vue'
import { recurrenceService } from '../services/recurrenceService'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const categories = useCategoriesStore()
const accounts = useAccountsStore()
const cards = useCreditCardsStore()

const items = ref<RecurrenceDto[]>([])
const loading = ref(false)
const loaded = ref(false)
const error = ref<string | null>(null)
const showInactive = ref(false)
const busy = ref(false)
let sequence = 0

const formVisible = ref(false)
const editing = ref<RecurrenceDto | null>(null)
const confirmVisible = ref(false)
const toggling = ref<RecurrenceDto | null>(null)

const hasInactive = computed(() => items.value.some((r) => !r.isActive))

async function load(): Promise<void> {
  const current = ++sequence
  loading.value = true
  error.value = null
  try {
    // Sempre inclui inativas: o filtro "mostrar inativas" é aplicado na tela (e `?open` pode apontar para uma).
    const result = await recurrenceService.list({ includeInactive: true })
    if (current !== sequence) return
    items.value = result
    loaded.value = true
  } catch (e) {
    if (current === sequence) error.value = toAppError(e).message
  } finally {
    if (current === sequence) loading.value = false
  }
}

const visibleItems = computed(() => items.value.filter((r) => showInactive.value || r.isActive))

function openCreate(): void {
  editing.value = null
  formVisible.value = true
}

function openEdit(rule: RecurrenceDto): void {
  editing.value = rule
  formVisible.value = true
}

function viewOccurrences(rule: RecurrenceDto): void {
  void router.push({ name: 'transactions', query: { recurrenceRuleId: rule.id } })
}

// `?open=<id>` (link a partir de uma ocorrência) abre a regra para edição, uma única vez após carregar.
function consumeOpenParam(): void {
  const raw = Array.isArray(route.query.open) ? route.query.open[0] : route.query.open
  if (typeof raw !== 'string' || raw.length === 0 || !loaded.value) return
  const { open: _open, ...rest } = route.query
  void router.replace({ query: rest })
  const rule = items.value.find((r) => r.id === raw)
  if (rule) openEdit(rule)
  else toast.add({ severity: 'warn', summary: 'Recorrência não encontrada', life: 4000 })
}

watch(() => [loaded.value, route.query.open], consumeOpenParam)

function onToggle(rule: RecurrenceDto): void {
  toggling.value = rule
  confirmVisible.value = true
}

async function runToggle(): Promise<void> {
  const rule = toggling.value
  if (!rule) return
  busy.value = true
  try {
    if (rule.isActive) await recurrenceService.deactivate(rule.id)
    else await recurrenceService.activate(rule.id)
    toast.add({
      severity: 'success',
      summary: rule.isActive ? 'Recorrência desativada' : 'Recorrência ativada',
      life: 3500,
    })
    await load()
  } catch (e) {
    toast.add({ severity: 'error', summary: 'Não foi possível concluir', detail: toAppError(e).message, life: 6000 })
  } finally {
    confirmVisible.value = false
    busy.value = false
  }
}

const confirmCopy = computed(() =>
  toggling.value?.isActive
    ? {
        header: 'Desativar recorrência',
        message: `Desativar “${toggling.value.description}”? As ocorrências futuras sem pagamento e que nunca foram editadas manualmente serão removidas. Ocorrências pagas, vencidas, canceladas ou editadas permanecem. Você pode reativá-la depois.`,
        label: 'Desativar',
        severity: 'danger' as const,
      }
    : {
        header: 'Ativar recorrência',
        message: `Ativar “${toggling.value?.description ?? ''}”? As ocorrências futuras serão geradas novamente conforme a regra. Ocorrências que já existem permanecem como estão.`,
        label: 'Ativar',
        severity: 'primary' as const,
      },
)

onMounted(() => {
  void categories.load()
  void accounts.load()
  void cards.load()
  void load()
})
</script>

<template>
  <div>
    <PageHeader
      title="Recorrências"
      description="Receitas e despesas que se repetem. Cada ocorrência vira uma movimentação gerada pelo sistema."
    >
      <template #actions>
        <Button type="button" label="Nova recorrência" icon="pi pi-plus" @click="openCreate" />
      </template>
    </PageHeader>

    <div v-if="hasInactive" class="rec-page__filter">
      <Checkbox v-model="showInactive" input-id="show-inactive-rec" binary />
      <label for="show-inactive-rec">Mostrar recorrências inativas</label>
    </div>

    <LoadingState v-if="loading && !loaded" label="Carregando recorrências…" />

    <Message v-else-if="error && !loaded" severity="error" role="alert">
      <div class="rec-page__error">
        <span>{{ error }}</span>
        <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="load()" />
      </div>
    </Message>

    <EmptyState
      v-else-if="visibleItems.length === 0"
      icon="pi pi-sync"
      :title="items.length === 0 ? 'Nenhuma recorrência cadastrada' : 'Nenhuma recorrência ativa'"
      :description="
        items.length === 0
          ? 'Cadastre assinaturas, aluguel, salário e outras movimentações que se repetem para não precisar lançá-las uma a uma.'
          : 'Todas as suas recorrências estão inativas. Ative uma delas ou crie uma nova.'
      "
    >
      <Button type="button" label="Nova recorrência" icon="pi pi-plus" @click="openCreate" />
    </EmptyState>

    <RecurrenceList
      v-else
      :items="visibleItems"
      :busy="busy"
      @edit="openEdit"
      @toggle="onToggle"
      @occurrences="viewOccurrences"
    />

    <RecurrenceFormDialog v-model:visible="formVisible" :recurrence="editing" @saved="load()" />

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      :header="confirmCopy.header"
      :message="confirmCopy.message"
      :confirm-label="confirmCopy.label"
      :severity="confirmCopy.severity"
      :loading="busy"
      @confirm="runToggle"
    />
  </div>
</template>

<style scoped>
.rec-page__filter {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  margin-bottom: 1rem;
}

.rec-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
