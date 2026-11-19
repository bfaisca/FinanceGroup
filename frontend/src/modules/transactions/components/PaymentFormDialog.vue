<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useClockStore } from '@/stores/clock'
import type { CreatePaymentRequest, TransactionDetailDto } from '@/types/api'
import { paymentActionLabel } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import { transactionService } from '../services/transactionService'
import { PAYMENT_FORM_FIELDS } from '../validation'
import PaymentForm from './PaymentForm.vue'

const props = defineProps<{ transaction: TransactionDetailDto }>()
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [transaction: TransactionDetailDto] }>()

const toast = useToast()
const clock = useClockStore()
useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
const formKey = ref(0)

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
  }
})

async function onSubmit(request: CreatePaymentRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const saved = await transactionService.addPayment(props.transaction.id, request)
    visible.value = false
    toast.add({
      severity: 'success',
      summary: props.transaction.type === 'Income' ? 'Recebimento registrado' : 'Pagamento registrado',
      life: 3500,
    })
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (PAYMENT_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
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
    :header="paymentActionLabel(transaction.type)"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(32rem, calc(100vw - 1.5rem))' }"
  >
    <PaymentForm
      :key="formKey"
      :transaction="transaction"
      :today="clock.today"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
