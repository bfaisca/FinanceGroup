<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, onMounted, reactive, watch } from 'vue'
import Button from 'primevue/button'
import Message from 'primevue/message'
import Select from 'primevue/select'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import {
  buildPaymentRequest,
  defaultPaymentAmountText,
  PAYMENT_FORM_FIELDS,
  validatePaymentForm,
  type PaymentFormValues,
} from '@/modules/transactions/validation'
import { useAccountsStore } from '@/stores/accounts'
import { formatCurrency } from '@/utils/format'
import { enumOptions, PAYMENT_METHOD_LABELS, paymentActionLabel } from '@/utils/labels'
import { toCents } from '@/utils/money'
import type { CreateGroupPaymentRequest, GroupTransactionDetailDto } from '../types'

const props = withDefaults(
  defineProps<{
    transaction: GroupTransactionDetailDto
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: CreateGroupPaymentRequest]; cancel: [] }>()

const accounts = useAccountsStore()
const { errors, clear, setLocal, setServer } = useFormErrors(PAYMENT_FORM_FIELDS)

const isIncome = computed(() => props.transaction.type === 'Income')
const remainingCents = computed(() => toCents(props.transaction.remainingAmount))

// Padrões: valor = restante; data = hoje; sem conta (a conta é privada e opcional).
const form = reactive<PaymentFormValues>({
  amount: defaultPaymentAmountText(props.transaction.remainingAmount),
  paymentDate: props.today,
  accountId: null,
  paymentMethod: props.transaction.paymentMethod,
})

onMounted(() => {
  void accounts.load()
})

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const methodOptions = enumOptions(PAYMENT_METHOD_LABELS)
const accountOptions = computed(() => accounts.activeItems.map((a) => ({ value: a.id, label: a.name })))

function onSubmit(): void {
  const valid = setLocal(validatePaymentForm(form, { remainingCents: remainingCents.value, today: props.today }))
  if (!valid) return
  emit('submit', buildPaymentRequest(form))
}
</script>

<template>
  <form class="form-stack" novalidate :aria-label="paymentActionLabel(transaction.type)" @submit.prevent="onSubmit">
    <p class="gpay-form__summary">
      <strong>{{ transaction.description }}</strong><br />
      Restante: <span class="money">{{ formatCurrency(transaction.remainingAmount) }}</span> de
      <span class="money">{{ formatCurrency(transaction.amount) }}</span>
    </p>

    <Message :pt="NOTE_PT" severity="secondary" :closable="false" data-testid="own-payment-note">
      O {{ isIncome ? 'recebimento' : 'pagamento' }} será registrado em seu nome. Os demais participantes veem quem
      {{ isIncome ? 'recebeu' : 'pagou' }}, o valor e a data.
    </Message>

    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField
      id="gpay-amount"
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
      id="gpay-date"
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
        id="gpay-method"
        v-slot="{ id, describedBy, invalid }"
        label="Método"
        :error="errors.paymentMethod"
        hint="Opcional. Visível para o grupo."
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
      <FormField
        id="gpay-account"
        v-slot="{ id, describedBy, invalid }"
        label="Sua conta (privada)"
        :error="errors.accountId"
        hint="Opcional. Sua conta é privada: o grupo não a vê. Ela só afeta o seu saldo individual."
      >
        <Select
          v-model="form.accountId"
          :input-id="id"
          :options="accountOptions"
          option-label="label"
          option-value="value"
          placeholder="Não vincular a uma conta"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('accountId')"
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
.gpay-form__summary {
  margin: 0;
}
</style>
