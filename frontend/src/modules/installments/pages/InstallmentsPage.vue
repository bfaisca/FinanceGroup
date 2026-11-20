<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import EmptyState from '@/components/EmptyState.vue'
import FormField from '@/components/FormField.vue'
import LoadingState from '@/components/LoadingState.vue'
import MonthsSelect from '@/components/MonthsSelect.vue'
import PageHeader from '@/components/PageHeader.vue'
import { reportService } from '@/modules/reports/services/reportService'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import { useCreditCardsStore } from '@/stores/creditCards'
import type { InstallmentPlanDto, InstallmentPlanStatus, ReportInstallmentsDto } from '@/types/api'
import { INSTALLMENT_STATUS_PRESENTATION, INSTALLMENT_STATUSES } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import CommitmentSection from '../components/CommitmentSection.vue'
import InstallmentPlanDrawer from '../components/InstallmentPlanDrawer.vue'
import InstallmentPlanFormDialog from '../components/InstallmentPlanFormDialog.vue'
import InstallmentPlanList from '../components/InstallmentPlanList.vue'
import { useInstallmentPlanList } from '../composables'

const PAGE_SIZE = 10

const route = useRoute()
const router = useRouter()
const categories = useCategoriesStore()
const accounts = useAccountsStore()
const cards = useCreditCardsStore()

function single(value: unknown): string | undefined {
  const first = Array.isArray(value) ? value[0] : value
  return typeof first === 'string' && first.length > 0 ? first : undefined
}

// A URL guarda `status`, `page` e `open` (links compartilháveis).
const status = computed(() => {
  const value = single(route.query.status)
  return value && (INSTALLMENT_STATUSES as string[]).includes(value) ? (value as InstallmentPlanStatus) : undefined
})
const page = computed(() => {
  const value = Number(single(route.query.page))
  return Number.isSafeInteger(value) && value > 0 ? value : 1
})
const openId = computed(() => single(route.query.open) ?? null)

const list = useInstallmentPlanList(() => ({ page: page.value, pageSize: PAGE_SIZE, status: status.value }))

function patchQuery(patch: Record<string, string | undefined>): void {
  const next: Record<string, string> = {}
  for (const [key, value] of Object.entries({ ...route.query, ...patch })) {
    const v = single(value)
    if (v !== undefined) next[key] = v
  }
  void router.replace({ query: next })
}

const statusOptions = [
  { value: null as InstallmentPlanStatus | null, label: 'Todas as situações' },
  ...INSTALLMENT_STATUSES.map((value) => ({
    value: value as InstallmentPlanStatus | null,
    label: INSTALLMENT_STATUS_PRESENTATION[value].label,
  })),
]

function setStatus(value: InstallmentPlanStatus | null): void {
  patchQuery({ status: value ?? undefined, page: undefined })
}

// ---- Detalhe ----
const detailVisible = computed({
  get: () => openId.value !== null,
  set: (value: boolean) => {
    if (!value) patchQuery({ open: undefined })
  },
})

function openPlan(plan: InstallmentPlanDto): void {
  patchQuery({ open: plan.id })
}

// ---- Criação ----
const formVisible = ref(false)

function onCreated(): void {
  void list.reload()
  void loadCommitment()
}

function onChanged(): void {
  void list.reload()
  void loadCommitment()
}

// Se a página atual deixou de existir (ex.: excluiu o último item dela), volta à última.
watch(
  () => [list.totalPages.value, list.loading.value] as const,
  ([totalPages, loading]) => {
    if (!loading && totalPages > 0 && page.value > totalPages) patchQuery({ page: String(totalPages) })
  },
)

// ---- Comprometimento futuro ----
const months = ref(12)
const commitment = ref<ReportInstallmentsDto | null>(null)
const commitmentLoading = ref(false)
const commitmentError = ref<string | null>(null)
let commitmentSeq = 0

async function loadCommitment(): Promise<void> {
  const current = ++commitmentSeq
  commitmentLoading.value = true
  commitmentError.value = null
  try {
    const result = await reportService.installments({ months: months.value })
    if (current === commitmentSeq) commitment.value = result
  } catch (e) {
    if (current === commitmentSeq) commitmentError.value = toAppError(e).message
  } finally {
    if (current === commitmentSeq) commitmentLoading.value = false
  }
}

watch(months, () => void loadCommitment())

onMounted(() => {
  void categories.load()
  void accounts.load()
  void cards.load()
  void loadCommitment()
})
</script>

<template>
  <div class="installments-page">
    <PageHeader title="Parcelamentos" description="Compras parceladas, o que já foi pago e o comprometimento futuro.">
      <template #actions>
        <Button type="button" label="Novo parcelamento" icon="pi pi-plus" @click="formVisible = true" />
      </template>
    </PageHeader>

    <section class="installments-page__filters" aria-label="Filtros">
      <FormField id="plan-filter-status" v-slot="{ id }" label="Situação">
        <Select
          :input-id="id"
          :model-value="status ?? null"
          :options="statusOptions"
          option-label="label"
          option-value="value"
          placeholder="Todas as situações"
          @update:model-value="setStatus"
        />
      </FormField>
    </section>

    <InstallmentPlanList
      :items="list.items.value"
      :loading="list.loading.value"
      :error="list.error.value"
      :total-count="list.totalCount.value"
      :page="page"
      :page-size="PAGE_SIZE"
      :filtered="status !== undefined"
      @open="openPlan"
      @create="formVisible = true"
      @retry="list.reload()"
      @update:page="patchQuery({ page: $event > 1 ? String($event) : undefined })"
    />

    <section class="surface-card installments-page__commitment" aria-labelledby="commitment-title">
      <div class="installments-page__commitment-header">
        <h2 id="commitment-title">Comprometimento futuro</h2>
        <MonthsSelect v-model="months" label="Janela" aria-label="Meses do comprometimento futuro" />
      </div>
      <LoadingState v-if="commitmentLoading && !commitment" label="Carregando comprometimento…" />
      <Message v-else-if="commitmentError && !commitment" severity="error" role="alert">
        <div class="installments-page__error">
          <span>{{ commitmentError }}</span>
          <Button type="button" label="Tentar novamente" icon="pi pi-refresh" size="small" @click="loadCommitment()" />
        </div>
      </Message>
      <EmptyState
        v-else-if="commitment && commitment.openPlans === 0 && commitment.totalRemaining === 0"
        icon="pi pi-chart-bar"
        title="Nenhum parcelamento em aberto"
        description="Quando houver parcelas a pagar, o comprometimento por mês aparece aqui."
      />
      <CommitmentSection v-else-if="commitment" :report="commitment" />
    </section>

    <InstallmentPlanFormDialog
      v-model:visible="formVisible"
      @saved="onCreated"
      @open-plan="patchQuery({ open: $event })"
    />
    <InstallmentPlanDrawer v-model:visible="detailVisible" :plan-id="openId" @changed="onChanged" />
  </div>
</template>

<style scoped>
.installments-page {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.installments-page__filters {
  max-width: 18rem;
}

.installments-page__commitment-header {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.installments-page__commitment-header h2 {
  font-size: 1.0625rem;
  font-weight: 600;
}

.installments-page__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}
</style>
