<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Message from 'primevue/message'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import LoadingState from '@/components/LoadingState.vue'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import TransactionDetailDrawer from '@/modules/transactions/components/TransactionDetailDrawer.vue'
import TransactionFormDialog from '@/modules/transactions/components/TransactionFormDialog.vue'
import type { InstallmentPlanDetailDto, TransactionDetailDto, TransactionDto } from '@/types/api'
import { formatCurrency, formatDate } from '@/utils/format'
import { paymentMethodLabel } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import { installmentPlanService } from '../services/installmentPlanService'
import InstallmentList from './InstallmentList.vue'
import InstallmentStatusChip from './InstallmentStatusChip.vue'
import PlanProgress from './PlanProgress.vue'

const props = defineProps<{ planId: string | null }>()
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ changed: [] }>()

const toast = useToast()
useRestoreFocus(visible)

const plan = ref<InstallmentPlanDetailDto | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const busy = ref(false)
let sequence = 0

type Pending = 'cancel' | 'delete'
const confirmVisible = ref(false)
const pending = ref<Pending | null>(null)

const txDetailVisible = ref(false)
const txDetailId = ref<string | null>(null)
const txRefresh = ref(0)
const txFormVisible = ref(false)
const editingTx = ref<TransactionDetailDto | null>(null)

const canCancel = computed(() => plan.value?.status === 'Open')

async function load(id: string): Promise<void> {
  const current = ++sequence
  loading.value = true
  error.value = null
  try {
    const result = await installmentPlanService.get(id)
    if (current === sequence) plan.value = result
  } catch (e) {
    if (current === sequence) {
      plan.value = null
      error.value = toAppError(e).message
    }
  } finally {
    if (current === sequence) loading.value = false
  }
}

watch(
  () => [visible.value, props.planId] as const,
  ([isVisible, id]) => {
    if (isVisible && id) void load(id)
    else if (!isVisible) {
      sequence++
      plan.value = null
      error.value = null
      loading.value = false
    }
  },
  { immediate: true },
)

const confirmCopy = computed(() =>
  pending.value === 'delete'
    ? {
        header: 'Excluir plano de parcelamento',
        message:
          'O plano e todas as suas parcelas serão excluídos definitivamente. Isso só é possível se nenhuma parcela tiver pagamento registrado; caso contrário, cancele o plano. Esta ação não pode ser desfeita.',
        label: 'Excluir plano',
      }
    : {
        header: 'Cancelar plano de parcelamento',
        message:
          'As parcelas sem pagamento serão canceladas e deixam de contar nos totais. Parcelas que já têm pagamento registrado permanecem como estão.',
        label: 'Cancelar plano',
      },
)

function ask(kind: Pending): void {
  pending.value = kind
  confirmVisible.value = true
}

async function runConfirmed(): Promise<void> {
  const current = plan.value
  if (!current || !pending.value) return
  busy.value = true
  try {
    if (pending.value === 'cancel') {
      plan.value = await installmentPlanService.cancel(current.id)
      toast.add({ severity: 'success', summary: 'Plano cancelado', life: 3500 })
    } else {
      await installmentPlanService.remove(current.id)
      toast.add({ severity: 'success', summary: 'Plano excluído', life: 3500 })
      visible.value = false
    }
    confirmVisible.value = false
    emit('changed')
  } catch (e) {
    // 422: o backend explica o motivo (ex.: há parcelas com pagamento) em `detail`.
    toast.add({
      severity: 'error',
      summary: 'Não foi possível concluir a ação',
      detail: toAppError(e).message,
      life: 7000,
    })
    confirmVisible.value = false
    void load(current.id)
  } finally {
    busy.value = false
  }
}

function openInstallment(tx: TransactionDto): void {
  txDetailId.value = tx.id
  txDetailVisible.value = true
}

function onTxChanged(): void {
  emit('changed')
  if (props.planId) void load(props.planId)
}

function onEditTx(tx: TransactionDetailDto): void {
  editingTx.value = tx
  txFormVisible.value = true
}

function onTxSaved(): void {
  txRefresh.value += 1
  onTxChanged()
}
</script>

<template>
  <Drawer
    v-model:visible="visible"
    position="right"
    :style="{ width: 'min(46rem, 100vw)' }"
    :block-scroll="true"
    aria-label="Detalhes do parcelamento"
  >
    <template #header>
      <h2 class="plan-drawer__title">{{ plan?.description ?? 'Parcelamento' }}</h2>
    </template>

    <LoadingState v-if="loading && !plan" label="Carregando parcelamento…" />

    <Message v-else-if="error" severity="error" role="alert">
      <div class="plan-drawer__error">
        <span>{{ error }}</span>
        <Button
          type="button"
          label="Tentar novamente"
          icon="pi pi-refresh"
          size="small"
          @click="planId && load(planId)"
        />
      </div>
    </Message>

    <div v-else-if="plan" class="stack">
      <div class="plan-drawer__headline">
        <InstallmentStatusChip :status="plan.status" />
        <PlanProgress
          :paid-count="plan.paidCount"
          :installment-count="plan.installmentCount"
          :description="plan.description"
        />
      </div>

      <dl class="plan-drawer__facts" data-testid="plan-summary">
        <div>
          <dt>Valor total</dt>
          <dd class="money">{{ formatCurrency(plan.totalAmount) }}</dd>
        </div>
        <div>
          <dt>Pago</dt>
          <dd class="money">{{ formatCurrency(plan.paidAmount) }}</dd>
        </div>
        <div>
          <dt>Restante</dt>
          <dd class="money">{{ formatCurrency(plan.remainingAmount) }}</dd>
        </div>
        <div>
          <dt>Próximo vencimento</dt>
          <dd>{{ formatDate(plan.nextDueDate) }}</dd>
        </div>
        <div>
          <dt>Parcelas</dt>
          <dd>
            {{ plan.installmentCount }} no total · {{ plan.paidCount }} pagas · {{ plan.openCount }} em aberto<template
              v-if="plan.canceledCount > 0"
            >
              · {{ plan.canceledCount }} canceladas</template
            >
          </dd>
        </div>
        <div>
          <dt>Primeira parcela</dt>
          <dd>{{ formatDate(plan.firstDate) }}</dd>
        </div>
        <div>
          <dt>Categoria</dt>
          <dd>{{ plan.categoryName }}</dd>
        </div>
        <div v-if="plan.creditCardName">
          <dt>Cartão de crédito</dt>
          <dd>{{ plan.creditCardName }}</dd>
        </div>
        <div v-else>
          <dt>Conta / método</dt>
          <dd>
            {{
              [plan.accountName, plan.paymentMethod ? paymentMethodLabel(plan.paymentMethod) : null]
                .filter(Boolean)
                .join(' · ') || '—'
            }}
          </dd>
        </div>
      </dl>

      <section aria-labelledby="plan-installments-title">
        <h3 id="plan-installments-title" class="plan-drawer__section-title">Parcelas</h3>
        <InstallmentList :installments="plan.installments" caption="Parcelas do plano" @open="openInstallment" />
      </section>

      <p class="plan-drawer__hint">
        <i class="pi pi-info-circle" aria-hidden="true" />
        Cancelar o plano não remove parcelas já pagas. Excluir só é possível quando nenhuma parcela foi paga.
      </p>

      <div class="plan-drawer__actions">
        <Button
          v-if="canCancel"
          type="button"
          label="Cancelar plano"
          icon="pi pi-ban"
          severity="secondary"
          outlined
          :disabled="busy"
          @click="ask('cancel')"
        />
        <Button
          type="button"
          label="Excluir plano"
          icon="pi pi-trash"
          severity="danger"
          outlined
          :disabled="busy"
          @click="ask('delete')"
        />
      </div>
    </div>

    <TransactionDetailDrawer
      v-model:visible="txDetailVisible"
      :transaction-id="txDetailId"
      :refresh-token="txRefresh"
      @changed="onTxChanged"
      @edit="onEditTx"
    />
    <TransactionFormDialog v-model:visible="txFormVisible" :transaction="editingTx" @saved="onTxSaved" />

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      :header="confirmCopy.header"
      :message="confirmCopy.message"
      :confirm-label="confirmCopy.label"
      severity="danger"
      :loading="busy"
      @confirm="runConfirmed"
    />
  </Drawer>
</template>

<style scoped>
.plan-drawer__title {
  font-size: 1.125rem;
  font-weight: 700;
  overflow-wrap: anywhere;
}

.plan-drawer__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.plan-drawer__headline {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
  align-items: center;
  justify-content: space-between;
}

.plan-drawer__facts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
  gap: 0.75rem 1rem;
  margin: 0;
}

.plan-drawer__facts dt {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.plan-drawer__facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.plan-drawer__section-title {
  font-size: 0.9375rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
}

.plan-drawer__hint {
  margin: 0;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}

.plan-drawer__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--gf-border);
}
</style>
