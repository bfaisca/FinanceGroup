<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { useAccountsStore } from '@/stores/accounts'
import type { CreatePaymentRequest, TransactionDetailDto } from '@/types/api'
import { formatCurrency } from '@/utils/format'
import { enumOptions, PAYMENT_METHOD_LABELS, paymentActionLabel } from '@/utils/labels'
import { toCents } from '@/utils/money'
import {
  buildPaymentRequest,
  defaultPaymentAmountText,
  PAYMENT_FORM_FIELDS,
  validatePaymentForm,
  type PaymentFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    transaction: TransactionDetailDto
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: CreatePaymentRequest]; cancel: [] }>()

const accounts = useAccountsStore()
const { errors, clear, setLocal, setServer } = useFormErrors(PAYMENT_FORM_FIELDS)

const isIncome = computed(() => props.transaction.type === 'Income')
const remainingCents = computed(() => toCents(props.transaction.remainingAmount))

// Padrões: valor = restante; data = hoje; conta e método da própria movimentação (se houver).
const form = reactive<PaymentFormValues>({
  amount: defaultPaymentAmountText(props.transaction.remainingAmount),
  paymentDate: props.today,
  accountId: props.transaction.accountId,
  paymentMethod: props.transaction.paymentMethod,
})

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const methodOptions = enumOptions(PAYMENT_METHOD_LABELS)
const accountOptions = computed(() =>
  accounts.items
    .filter((a) => a.isActive || a.id === props.transaction.accountId)
    .map((a) => ({ value: a.id, label: a.isActive ? a.name : `${a.name} (inativa)` })),
)

function onSubmit(): void {
  const valid = setLocal(validatePaymentForm(form, { remainingCents: remainingCents.value, today: props.today }))
  if (!valid) return
  emit('submit', buildPaymentRequest(form))
}
</script>

<template>
  <form class="form-stack" novalidate :aria-label="paymentActionLabel(transaction.type)" @submit.prevent="onSubmit">
    <p class="payment-form__summary">
      <strong>{{ transaction.description }}</strong><br />
      Restante: <span class="money">{{ formatCurrency(transaction.remainingAmount) }}</span> de
      <span class="money">{{ formatCurrency(transaction.amount) }}</span>
    </p>

    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField
      id="pay-amount"
      v-slot="{ id, describedBy, invalid }"
      :label="isIncome ? 'Valor recebido' : 'Valor pago'"
      :error="errors.amount"
      hint="Pagamentos parciais são permitidos; o valor não pode passar do restante."
      required
    >
      <MoneyInput
        :id="id"
        v-model="form.amount"
        :described-by="describedBy"
        :invalid="invalid"
        autofocus
        @update:model-value="clear('amount')"
      />
    </FormField>

    <FormField
      id="pay-date"
      v-slot="{ id, describedBy, invalid }"
      label="Data"
      :error="errors.paymentDate"
      hint="Não pode ser uma data futura."
      required
    >
      <DateInput
        :id="id"
        v-model="form.paymentDate"
        :described-by="describedBy"
        :invalid="invalid"
        :max="today"
        @update:model-value="clear('paymentDate')"
      />
    </FormField>

    <div class="form-grid">
      <FormField
        id="pay-account"
        v-slot="{ id, describedBy, invalid }"
        label="Conta"
        :error="errors.accountId"
        hint="Opcional."
      >
        <Select
          v-model="form.accountId"
          :input-id="id"
          :options="accountOptions"
          option-label="label"
          option-value="value"
          placeholder="Sem conta"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('accountId')"
        />
      </FormField>
      <FormField
        id="pay-method"
        v-slot="{ id, describedBy, invalid }"
        label="Método"
        :error="errors.paymentMethod"
        hint="Opcional."
      >
        <Select
          v-model="form.paymentMethod"
          :input-id="id"
          :options="methodOptions"
          option-label="label"
          option-value="value"
          placeholder="Não informado"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('paymentMethod')"
        />
      </FormField>
    </div>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button type="submit" :label="paymentActionLabel(transaction.type)" icon="pi pi-check" :loading="loading" />
    </div>
  </form>
</template>

<style scoped>
.payment-form__summary {
  margin: 0;
}
</style>
