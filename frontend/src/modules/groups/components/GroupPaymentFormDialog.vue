<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import Dialog from 'primevue/dialog'
import { useToast } from 'primevue/usetoast'
import { useRestoreFocus } from '@/composables/useRestoreFocus'
import { useClockStore } from '@/stores/clock'
import { toAppError } from '@/utils/problem'
import { groupTransactionService } from '../services/groupTransactionService'
import type { GroupPaymentRequest, GroupTransactionDetailDto, GroupTransactionPaymentDto } from '../types'
import { GROUP_PAYMENT_FORM_FIELDS } from '../validation'
import GroupPaymentForm from './GroupPaymentForm.vue'

const props = withDefaults(
  defineProps<{
    groupId: string
    transaction: GroupTransactionDetailDto
    /** Pagamento em edição (só o próprio pagador); `null` = novo pagamento. */
    payment?: GroupTransactionPaymentDto | null
  }>(),
  { payment: null },
)
const visible = defineModel<boolean>('visible', { required: true })
const emit = defineEmits<{ saved: [transaction: GroupTransactionDetailDto] }>()

const toast = useToast()
const clock = useClockStore()
useRestoreFocus(visible)

const loading = ref(false)
const errorMessage = ref<string | null>(null)
const fieldErrors = ref<Record<string, string[]>>({})
const formKey = ref(0)

const isEdit = computed(() => props.payment !== null)

watch(visible, (value) => {
  if (value) {
    formKey.value += 1
    errorMessage.value = null
    fieldErrors.value = {}
  }
})

async function onSubmit(request: GroupPaymentRequest): Promise<void> {
  loading.value = true
  errorMessage.value = null
  fieldErrors.value = {}
  try {
    const saved = props.payment
      ? await groupTransactionService.updatePayment(props.groupId, props.transaction.id, props.payment.id, request)
      : await groupTransactionService.addPayment(props.groupId, props.transaction.id, request)
    visible.value = false
    toast.add({
      severity: 'success',
      summary: isEdit.value ? 'Pagamento atualizado' : 'Pagamento registrado',
      life: 3500,
    })
    emit('saved', saved)
  } catch (error) {
    const appError = toAppError(error)
    fieldErrors.value = appError.fieldErrors
    const mapped = (GROUP_PAYMENT_FORM_FIELDS as readonly string[]).some((field) => appError.fieldErrors[field])
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
    :header="isEdit ? 'Editar pagamento' : 'Registrar pagamento'"
    :closable="!loading"
    :close-on-escape="!loading"
    :style="{ width: 'min(34rem, calc(100vw - 1.5rem))' }"
  >
    <GroupPaymentForm
      :key="formKey"
      :transaction="transaction"
      :payment="payment"
      :today="clock.today"
      :loading="loading"
      :error-message="errorMessage"
      :field-errors="fieldErrors"
      @submit="onSubmit"
      @cancel="visible = false"
    />
  </Dialog>
</template>
