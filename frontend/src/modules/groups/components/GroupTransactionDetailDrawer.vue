<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Button from 'primevue/button'
import Drawer from 'primevue/drawer'
import Message from 'primevue/message'
import ProgressBar from 'primevue/progressbar'
import Tab from 'primevue/tab'
import TabList from 'primevue/tablist'
import TabPanel from 'primevue/tabpanel'
import TabPanels from 'primevue/tabpanels'
import Tabs from 'primevue/tabs'
import { useToast } from 'primevue/usetoast'
import AppConfirmDialog from '@/components/AppConfirmDialog.vue'
import CategoryBadge from '@/components/CategoryBadge.vue'
import LoadingState from '@/components/LoadingState.vue'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { paidPercentage } from '@/modules/transactions/actions'
import TransactionAmount from '@/modules/transactions/components/TransactionAmount.vue'
import TransactionStatusChip from '@/modules/transactions/components/TransactionStatusChip.vue'
import { formatCurrency, formatDate } from '@/utils/format'
import { PAYMENT_METHOD_LABELS } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import { groupTransactionActions } from '../actions'
import { useGroupTransactionDetail } from '../composables'
import { groupTransactionService } from '../services/groupTransactionService'
import type { GroupTransactionDetailDto, GroupTransactionPaymentDto } from '../types'
import GroupPaidSummary from './GroupPaidSummary.vue'
import GroupPaymentFormDialog from './GroupPaymentFormDialog.vue'
import GroupSeriesBadge from './GroupSeriesBadge.vue'
import GroupTransactionHistoryList from './GroupTransactionHistoryList.vue'

type PendingConfirm =
  { kind: 'cancel' } | { kind: 'delete' } | { kind: 'removePayment'; payment: GroupTransactionPaymentDto }

const props = withDefaults(defineProps<{ groupId: string; transactionId: string | null; refreshToken?: number }>(), {
  refreshToken: 0,
})
const visible = defineModel<boolean>('visible', { required: true })

const emit = defineEmits<{
  /** Algo mudou (pagamento, cancelamento, exclusão…): a lista deve ser recarregada. */
  changed: []
  edit: [transaction: GroupTransactionDetailDto]
}>()

const toast = useToast()
useRestoreFocus(visible)

const state = useGroupTransactionDetail(() => props.groupId)
const { detail, loading, error, history, historyLoading, historyError, historyLoaded } = state

const activeTab = ref('details')
const paymentDialogVisible = ref(false)
/** Pagamento em edição (o próprio); `null` = novo pagamento. */
const editingPayment = ref<GroupTransactionPaymentDto | null>(null)
const confirmVisible = ref(false)
const pending = ref<PendingConfirm | null>(null)
const busy = ref(false)

const actions = computed(() => (detail.value ? groupTransactionActions(detail.value) : null))
const percentage = computed(() => (detail.value ? paidPercentage(detail.value.paidAmount, detail.value.amount) : 0))

watch(
  () => [visible.value, props.transactionId] as const,
  ([isVisible, id]) => {
    if (isVisible && id) {
      activeTab.value = 'details'
      void state.load(id)
    } else if (!isVisible) {
      state.reset()
    }
  },
  { immediate: true },
)

watch(
  () => props.refreshToken,
  () => {
    if (visible.value && props.transactionId) void state.load(props.transactionId)
  },
)

watch(activeTab, (tab) => {
  if (tab === 'history' && !historyLoaded.value && !historyLoading.value) void state.loadHistory()
})

const confirmCopy = computed(() => {
  const current = pending.value
  if (!current) return { header: '', message: '', label: 'Confirmar' }
  switch (current.kind) {
    case 'cancel':
      return {
        header: 'Cancelar movimentação',
        message:
          'A movimentação deixará de contar nos totais do grupo. Os pagamentos já registrados são preservados e você poderá reabri-la depois.',
        label: 'Cancelar movimentação',
      }
    case 'delete':
      return {
        header: 'Excluir movimentação',
        message: 'A movimentação será excluída definitivamente para todo o grupo. Esta ação não pode ser desfeita.',
        label: 'Excluir',
      }
    default:
      return {
        header: 'Remover pagamento',
        message: `Remover o seu pagamento de ${formatCurrency(current.payment.totalAmount)} de ${formatDate(current.payment.paymentDate)}? A situação da movimentação será recalculada.`,
        label: 'Remover pagamento',
      }
  }
})

function openNewPayment(): void {
  editingPayment.value = null
  paymentDialogVisible.value = true
}

function openEditPayment(payment: GroupTransactionPaymentDto): void {
  editingPayment.value = payment
  paymentDialogVisible.value = true
}

function ask(confirm: PendingConfirm): void {
  pending.value = confirm
  confirmVisible.value = true
}

function notifyError(summary: string, e: unknown): void {
  toast.add({ severity: 'error', summary, detail: toAppError(e).message, life: 6000 })
}

async function runConfirmed(): Promise<void> {
  const current = pending.value
  const tx = detail.value
  if (!current || !tx) return
  busy.value = true
  try {
    if (current.kind === 'cancel') {
      state.apply(await groupTransactionService.cancel(props.groupId, tx.id))
      toast.add({ severity: 'success', summary: 'Movimentação cancelada', life: 3500 })
    } else if (current.kind === 'removePayment') {
      state.apply(await groupTransactionService.removePayment(props.groupId, tx.id, current.payment.id))
      toast.add({ severity: 'success', summary: 'Pagamento removido', life: 3500 })
    } else {
      await groupTransactionService.remove(props.groupId, tx.id)
      toast.add({ severity: 'success', summary: 'Movimentação excluída', life: 3500 })
      confirmVisible.value = false
      visible.value = false
    }
    confirmVisible.value = false
    emit('changed')
    if (activeTab.value === 'history' && current.kind !== 'delete') void state.loadHistory()
  } catch (e) {
    // 403/422 trazem a explicação em `detail`.
    notifyError('Não foi possível concluir a ação', e)
    confirmVisible.value = false
    // O estado pode ter mudado no servidor: recarrega para refletir a realidade.
    void state.load(tx.id)
  } finally {
    busy.value = false
  }
}

async function reopen(): Promise<void> {
  const tx = detail.value
  if (!tx) return
  busy.value = true
  try {
    state.apply(await groupTransactionService.reopen(props.groupId, tx.id))
    toast.add({ severity: 'success', summary: 'Movimentação reaberta', life: 3500 })
    emit('changed')
  } catch (e) {
    notifyError('Não foi possível reabrir', e)
  } finally {
    busy.value = false
  }
}

function onPaymentSaved(updated: GroupTransactionDetailDto): void {
  state.apply(updated)
  emit('changed')
}

const confirmSeverity = computed(() => (pending.value?.kind === 'removePayment' ? 'primary' : 'danger'))
</script>

<template>
  <Drawer
    v-model:visible="visible"
    position="right"
    :style="{ width: 'min(34rem, 100vw)' }"
    :block-scroll="true"
    aria-label="Detalhes da movimentação do grupo"
  >
    <template #header>
      <h2 class="gtx-drawer__title">{{ detail?.description ?? 'Movimentação do grupo' }}</h2>
    </template>

    <LoadingState v-if="loading" label="Carregando movimentação…" />

    <Message v-else-if="error" severity="error" role="alert">
      <div class="gtx-drawer__error">
        <span>{{ error }}</span>
        <Button
          type="button"
          label="Tentar novamente"
          icon="pi pi-refresh"
          size="small"
          @click="transactionId && state.load(transactionId)"
        />
      </div>
    </Message>

    <div v-else-if="detail && actions" class="gtx-drawer">
      <Tabs v-model:value="activeTab">
        <TabList>
          <Tab value="details">Detalhes</Tab>
          <Tab value="history">Histórico</Tab>
        </TabList>
        <TabPanels>
          <TabPanel value="details">
            <div class="stack">
              <div class="gtx-drawer__headline">
                <span class="gtx-drawer__amount"
                  ><TransactionAmount :amount="detail.amount" :type="detail.type"
                /></span>
                <span class="gtx-drawer__chips">
                  <GroupSeriesBadge v-if="detail.series" :series="detail.series" />
                  <TransactionStatusChip :status="detail.status" />
                </span>
              </div>

              <section aria-labelledby="gtx-progress-title">
                <h3 id="gtx-progress-title" class="gtx-drawer__section-title">Pagamento</h3>
                <ProgressBar :value="percentage" :show-value="false" :aria-label="`${percentage}% da conta paga`" />
                <p class="gtx-drawer__progress-text">
                  <GroupPaidSummary :amount="detail.amount" :paid="detail.totalPaid" :interest="detail.interestPaid" />
                  <span
                    >Restante: <strong class="money">{{ formatCurrency(detail.remainingAmount) }}</strong></span
                  >
                </p>
              </section>

              <dl class="gtx-drawer__facts">
                <div>
                  <dt>Cadastrada por</dt>
                  <dd data-testid="detail-created-by">{{ detail.createdBy.name }}</dd>
                </div>
                <div>
                  <dt>Categoria</dt>
                  <dd>
                    <CategoryBadge
                      :name="detail.categoryName"
                      :parent-name="detail.parentCategoryName"
                      :color="detail.categoryColor"
                      :icon="detail.categoryIcon"
                    />
                  </dd>
                </div>
                <div>
                  <dt>Método de pagamento</dt>
                  <dd>{{ detail.paymentMethod ? PAYMENT_METHOD_LABELS[detail.paymentMethod] : '—' }}</dd>
                </div>
                <div>
                  <dt>Data da movimentação</dt>
                  <dd>{{ formatDate(detail.transactionDate) }}</dd>
                </div>
                <div>
                  <dt>Vencimento</dt>
                  <dd>{{ formatDate(detail.dueDate) }}</dd>
                </div>
                <div v-if="detail.series">
                  <dt>Recorrência</dt>
                  <dd data-testid="detail-series">
                    Conta {{ detail.series.number }} de {{ detail.series.total }}. Cada conta da série é editada, paga
                    ou cancelada separadamente.
                  </dd>
                </div>
                <div v-if="detail.paidAt">
                  <dt>Paga em</dt>
                  <dd>{{ formatDate(detail.paidAt) }}</dd>
                </div>
                <div v-if="detail.notes" class="gtx-drawer__notes">
                  <dt>Observações</dt>
                  <dd>{{ detail.notes }}</dd>
                </div>
              </dl>

              <section aria-labelledby="gtx-payments-title">
                <h3 id="gtx-payments-title" class="gtx-drawer__section-title">Pagamentos</h3>
                <p v-if="detail.payments.length === 0" class="gtx-drawer__muted">Nenhum pagamento registrado.</p>
                <div v-else class="table-scroll">
                  <table class="gtx-drawer__payments">
                    <caption class="sr-only">
                      Pagamentos registrados
                    </caption>
                    <thead>
                      <tr>
                        <th scope="col">Quem pagou</th>
                        <th scope="col">Data</th>
                        <th scope="col" class="num">Valor da conta</th>
                        <th scope="col" class="num">Juros/multa</th>
                        <th scope="col" class="num">Total pago</th>
                        <th scope="col">Método</th>
                        <th scope="col"><span class="sr-only">Ações</span></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="payment in detail.payments" :key="payment.id">
                        <td>{{ payment.paidBy.name }}</td>
                        <td>{{ formatDate(payment.paymentDate) }}</td>
                        <td class="num money">{{ formatCurrency(payment.amount) }}</td>
                        <td class="num money">{{ formatCurrency(payment.interestAmount) }}</td>
                        <td class="num money">
                          <strong>{{ formatCurrency(payment.totalAmount) }}</strong>
                        </td>
                        <td>{{ payment.paymentMethod ? PAYMENT_METHOD_LABELS[payment.paymentMethod] : '—' }}</td>
                        <td class="gtx-drawer__row-actions">
                          <Button
                            v-if="payment.canEdit"
                            type="button"
                            icon="pi pi-pencil"
                            severity="secondary"
                            text
                            rounded
                            :aria-label="`Editar seu pagamento de ${formatCurrency(payment.totalAmount)} em ${formatDate(payment.paymentDate)}`"
                            :disabled="busy"
                            @click="openEditPayment(payment)"
                          />
                          <Button
                            v-if="payment.canRemove"
                            type="button"
                            icon="pi pi-trash"
                            severity="danger"
                            text
                            rounded
                            :aria-label="`Remover seu pagamento de ${formatCurrency(payment.totalAmount)} em ${formatDate(payment.paymentDate)}`"
                            :disabled="busy"
                            @click="ask({ kind: 'removePayment', payment })"
                          />
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
                <p class="gtx-drawer__muted gtx-drawer__rule">
                  Você pode registrar, editar e remover apenas o seu próprio pagamento.
                </p>
              </section>

              <p v-if="actions.deleteBlockedByPayments" class="gtx-drawer__muted" data-testid="delete-hint">
                <i class="pi pi-info-circle" aria-hidden="true" />
                Com pagamentos registrados não é possível excluir; você pode cancelar a movimentação.
              </p>

              <div class="gtx-drawer__actions">
                <Button
                  v-if="actions.canPay"
                  type="button"
                  label="Registrar pagamento"
                  icon="pi pi-check-circle"
                  :disabled="busy"
                  @click="openNewPayment"
                />
                <Button
                  v-if="actions.canEdit"
                  type="button"
                  label="Editar"
                  icon="pi pi-pencil"
                  severity="secondary"
                  :disabled="busy"
                  @click="emit('edit', detail)"
                />
                <Button
                  v-if="actions.canReopen"
                  type="button"
                  label="Reabrir"
                  icon="pi pi-replay"
                  :loading="busy"
                  @click="reopen"
                />
                <Button
                  v-if="actions.canCancel"
                  type="button"
                  label="Cancelar movimentação"
                  icon="pi pi-ban"
                  severity="secondary"
                  outlined
                  :disabled="busy"
                  @click="ask({ kind: 'cancel' })"
                />
                <Button
                  v-if="actions.canDelete"
                  type="button"
                  label="Excluir"
                  icon="pi pi-trash"
                  severity="danger"
                  outlined
                  :disabled="busy"
                  @click="ask({ kind: 'delete' })"
                />
              </div>
            </div>
          </TabPanel>

          <TabPanel value="history">
            <LoadingState v-if="historyLoading" label="Carregando histórico…" />
            <Message v-else-if="historyError" severity="error" role="alert">
              <div class="gtx-drawer__error">
                <span>{{ historyError }}</span>
                <Button
                  type="button"
                  label="Tentar novamente"
                  icon="pi pi-refresh"
                  size="small"
                  @click="state.loadHistory()"
                />
              </div>
            </Message>
            <p v-else-if="history.length === 0" class="gtx-drawer__muted">Nenhum registro de histórico.</p>
            <GroupTransactionHistoryList v-else :entries="history" />
          </TabPanel>
        </TabPanels>
      </Tabs>
    </div>

    <GroupPaymentFormDialog
      v-if="detail"
      v-model:visible="paymentDialogVisible"
      :group-id="groupId"
      :transaction="detail"
      :payment="editingPayment"
      @saved="onPaymentSaved"
    />

    <AppConfirmDialog
      v-model:visible="confirmVisible"
      :header="confirmCopy.header"
      :message="confirmCopy.message"
      :confirm-label="confirmCopy.label"
      :severity="confirmSeverity"
      :loading="busy"
      @confirm="runConfirmed"
    />
  </Drawer>
</template>

<style scoped>
.gtx-drawer__title {
  font-size: 1.125rem;
  font-weight: 700;
  overflow-wrap: anywhere;
}

.gtx-drawer__error {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.gtx-drawer__headline {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.gtx-drawer__chips {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: center;
}

.gtx-drawer__row-actions {
  white-space: nowrap;
}

.gtx-drawer__payments .num {
  text-align: right;
}

.gtx-drawer__amount {
  font-size: 1.5rem;
  font-weight: 700;
}

.gtx-drawer__section-title {
  font-size: 0.9375rem;
  font-weight: 600;
  margin-bottom: 0.5rem;
}

.gtx-drawer__progress-text {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.5rem;
  margin: 0.5rem 0 0;
}

.gtx-drawer__facts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr));
  gap: 0.75rem 1rem;
  margin: 0;
}

.gtx-drawer__facts dt {
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.gtx-drawer__facts dd {
  margin: 0;
  overflow-wrap: anywhere;
}

.gtx-drawer__notes {
  grid-column: 1 / -1;
  white-space: pre-wrap;
}

.gtx-drawer__muted {
  margin: 0;
  color: var(--gf-text-muted);
}

.gtx-drawer__rule {
  margin-top: 0.5rem;
  font-size: 0.8125rem;
}

.gtx-drawer__payments {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9375rem;
}

.gtx-drawer__payments th,
.gtx-drawer__payments td {
  padding: 0.5rem 0.5rem 0.5rem 0;
  text-align: left;
  border-bottom: 1px solid var(--gf-border);
  vertical-align: middle;
}

.gtx-drawer__payments th {
  font-weight: 600;
  font-size: 0.8125rem;
  color: var(--gf-text-muted);
}

.gtx-drawer__actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--gf-border);
}
</style>
