<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useClockStore } from '@/stores/clock'
import type {
  CreateRecurrenceRequest,
  CreateTransactionRequest,
  TransactionDetailDto,
  TransactionType,
  UpdateTransactionRequest,
} from '@/types/api'
import { formatDate } from '@/utils/format'
import { toAppError } from '@/utils/problem'
import { recurrenceService } from '@/modules/recurrence/services/recurrenceService'
import { transactionService } from '../services/transactionService'
import { TRANSACTION_FORM_FIELDS } from '../validation'
import TransactionForm from './TransactionForm.vue'

const props = withDefaults(
  defineProps<{
    /** `null` = criação. */
    transaction?: TransactionDetailDto | null
    defaultType?: TransactionType
    defaultAccountId?: string | null
  }>(),
  { transaction: null, defaultType: 'Expense', defaultAccountId: null },
)

const visible = defineModel<boolean>('visible', { required: true })
/** `null` quando foi criada uma receita fixa (recorrência): não há uma única movimentação para devolver. */
const emit = defineEmits<{ saved: [transaction: TransactionDetailDto | null] }>()

const toast = useToast()
const clock = useClockStore()
useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
/** Força recriar o formulário a cada abertura (estado limpo). */
const formKey = ref(0)

const isEdit = computed(() => props.transaction !== null)

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
  }
})

async function onSubmitRecurring(request: CreateRecurrenceRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    await recurrenceService.create(request)
    visible.value = false
    toast.add({
      severity: 'success',
      summary: 'Receita fixa criada',
      detail: 'Ela será lançada todo mês. Para encerrar, abra uma movimentação dela e use "Encerrar repetição".',
      life: 6000,
    })
    emit('saved', null)
  } catch (error) {
    const appError = toAppError(error)
    // Campos da recorrência (startDate, …) são mostrados no aviso: o formulário usa `transactionDate`.
    fieldErrors.value = appError.fieldErrors
    errorMessage.value = appError.message
  } finally {
    loading.value = false
  }
}

async function onSubmit(request: CreateTransactionRequest | UpdateTransactionRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const saved = props.transaction
      ? await transactionService.update(props.transaction.id, request as UpdateTransactionRequest)
      : await transactionService.create(request as CreateTransactionRequest)
    visible.value = false
    toast.add({
      severity: 'success',
      summary: isEdit.value ? 'Movimentação atualizada' : 'Movimentação criada',
      // Compra no cartão: o vencimento da fatura é calculado pelo backend e só exibido aqui.
      detail:
        saved.creditCardId && saved.dueDate
          ? `Vencimento da fatura: ${formatDate(saved.dueDate)}${saved.creditCardName ? ` (${saved.creditCardName})` : ''}.`
          : undefined,
      life: saved.creditCardId ? 6000 : 3500,
    })
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    // `errors` (400) vão para os campos; `detail`/`title` (422 etc.) para o aviso do formulário.
    fieldErrors.value = appError.fieldErrors
    const mapped = (TRANSACTION_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
    errorMessage.value = mapped ? null : appError.message
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="isEdit ? 'Editar movimentação' : 'Nova movimentação'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(40rem, calc(100vw - 1.5rem))' }"
  >
    <TransactionForm
      :key="formKey"
      :transaction="transaction"
      :default-type="defaultType"
      :default-account-id="defaultAccountId"
      :today="clock.today"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @submit-recurring="onSubmitRecurring"
      @cancel="visible = false"
    />
  </Dialog>
</template>
