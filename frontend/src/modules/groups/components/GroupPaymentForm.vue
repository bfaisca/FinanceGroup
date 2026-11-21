<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, onMounted, reactive, watch } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import Message from 'primevue/message'
import Select from 'primevue/select'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { defaultPaymentAmountText } from '@/modules/transactions/validation'
import { useAccountsStore } from '@/stores/accounts'
import { formatCurrency } from '@/utils/format'
import { enumOptions, PAYMENT_METHOD_LABELS } from '@/utils/labels'
import { formatMoneyInput, toCents } from '@/utils/money'
import type { GroupPaymentRequest, GroupTransactionDetailDto, GroupTransactionPaymentDto } from '../types'
import {
  buildGroupPaymentRequest,
  GROUP_PAYMENT_FORM_FIELDS,
  validateGroupPaymentForm,
  type GroupPaymentFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    transaction: GroupTransactionDetailDto
    /** Pagamento em edição; `null` = novo pagamento. */
    payment?: GroupTransactionPaymentDto | null
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { payment: null, loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: GroupPaymentRequest]; cancel: [] }>()

const accounts = useAccountsStore()
const { errors, clear, setLocal, setServer } = useFormErrors(GROUP_PAYMENT_FORM_FIELDS)

const isEdit = computed(() => props.payment !== null)

/** Ao editar, o principal do próprio pagamento volta a ficar disponível. */
const availableCents = computed(() => toCents(props.transaction.remainingAmount) + toCents(props.payment?.amount ?? 0))

function initialValues(): GroupPaymentFormValues {
  const payment = props.payment
  if (payment) {
    return {
      amount: formatMoneyInput(toCents(payment.amount)),
      interest: payment.interestAmount > 0 ? formatMoneyInput(toCents(payment.interestAmount)) : '',
      paymentDate: payment.paymentDate,
      accountId: null,
      clearAccount: false,
      paymentMethod: payment.paymentMethod,
    }
  }
  // Padrões: valor = restante; data = hoje; sem conta (a conta é privada e opcional).
  return {
    amount: defaultPaymentAmountText(props.transaction.remainingAmount),
    interest: '',
    paymentDate: props.today,
    accountId: null,
    paymentMethod: props.transaction.paymentMethod,
  }
}

const form = reactive<GroupPaymentFormValues>(initialValues())

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
  const valid = setLocal(validateGroupPaymentForm(form, { remainingCents: availableCents.value, today: props.today }))
  if (!valid) return
  emit('submit', buildGroupPaymentRequest(form))
}
</script>

<template>
  <form
    class="form-stack"
    novalidate
    :aria-label="isEdit ? 'Editar pagamento' : 'Registrar pagamento'"
    @submit.prevent="onSubmit"
  >
    <p class="gpay-form__summary">
      <strong>{{ transaction.description }}</strong><br />
      Restante: <span class="money">{{ formatCurrency(transaction.remainingAmount) }}</span> de
      <span class="money">{{ formatCurrency(transaction.amount) }}</span>
    </p>

    <Message :pt="NOTE_PT" severity="secondary" :closable="false" data-testid="own-payment-note">
      O pagamento é registrado em seu nome. Os demais participantes veem quem pagou, o valor, os juros e a data.
    </Message>

    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField
      id="gpay-amount"
      v-slot="{ id, describedBy, invalid }"
      label="Valor da conta"
      :error="errors.amount"
      :hint="`Quanto da conta este pagamento cobre (sem juros). Pagamentos parciais são permitidos; no máximo ${formatCurrency(availableCents / 100)}.`"
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
      id="gpay-interest"
      v-slot="{ id, describedBy, invalid }"
      label="Juros/multa"
      :error="errors.interestAmount"
      hint="Opcional. Pagou atrasado com juros? Informe o valor da conta e os juros separados."
    >
      <MoneyInput
        :id="id"
        v-model="form.interest"
        :described-by="describedBy"
        :invalid="invalid"
        @update:model-value="clear('interestAmount')"
      />
    </FormField>

    <FormField
      id="gpay-date"
      v-slot="{ id, describedBy, invalid }"
      label="Data"
      :error="errors.paymentDate"
      hint="Pode ser qualquer data passada; não pode ser futura."
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
        :hint="
          isEdit
            ? 'Privada: o grupo não a vê e ela só afeta o seu saldo individual. Por ser privada, não é exibida aqui: deixe em branco para manter o vínculo atual, escolha outra conta para trocar ou marque a opção abaixo para desvincular.'
            : 'Opcional. Sua conta é privada: o grupo não a vê. Ela só afeta o seu saldo individual.'
        "
      >
        <Select
          v-model="form.accountId"
          :input-id="id"
          :options="accountOptions"
          option-label="label"
          option-value="value"
          :placeholder="isEdit ? 'Manter a conta atual' : 'Não vincular a uma conta'"
          show-clear
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('accountId')"
        />
        <label v-if="isEdit" class="gpay-clear">
          <Checkbox
            v-model="form.clearAccount"
            binary
            :disabled="form.accountId !== null"
            input-id="gpay-clear-account"
          />
          <span>Desvincular este pagamento da minha conta</span>
        </label>
      </FormField>
    </div>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button
        type="submit"
        :label="isEdit ? 'Salvar pagamento' : 'Registrar pagamento'"
        icon="pi pi-check"
        :loading="loading"
      />
    </div>
  </form>
</template>

<style scoped>
.gpay-form__summary {
  margin: 0;
}

.gpay-clear {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-top: 0.5rem;
  font-size: 0.875rem;
}
</style>
