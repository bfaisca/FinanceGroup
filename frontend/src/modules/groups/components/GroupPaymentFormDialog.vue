<script setup lang="ts">
import { ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { PAYMENT_FORM_FIELDS } from '@/modules/transactions/validation'
import { useClockStore } from '@/stores/clock'
import { paymentActionLabel } from '@/utils/labels'
import { toAppError } from '@/utils/problem'
import { groupTransactionService } from '../services/groupTransactionService'
import type { CreateGroupPaymentRequest, GroupTransactionDetailDto } from '../types'
import GroupPaymentForm from './GroupPaymentForm.vue'

const props = defineProps<{ groupId: string; transaction: GroupTransactionDetailDto }>()
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [transaction: GroupTransactionDetailDto] }>()

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

async function onSubmit(request: CreateGroupPaymentRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const saved = await groupTransactionService.addPayment(props.groupId, props.transaction.id, request)
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
    :style="{ width: 'min(34rem, calc(100vw - 1.5rem))' }"
  >
    <GroupPaymentForm
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
