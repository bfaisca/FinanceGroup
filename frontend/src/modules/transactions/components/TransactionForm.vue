<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import Checkbox from 'primevue/checkbox'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import Textarea from 'primevue/textarea'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import PaymentSourceFields from '@/components/PaymentSourceFields.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { buildCategoryOptions } from '@/modules/categories/tree'
import { useCategoriesStore } from '@/stores/categories'
import type {
  CreateRecurrenceRequest,
  CreateTransactionRequest,
  TransactionDetailDto,
  TransactionInitialStatus,
  TransactionType,
  UpdateTransactionRequest,
} from '@/types/api'
import { formatMoneyInput, toCents } from '@/utils/money'
import { enumOptions, STATUS_PRESENTATION, TRANSACTION_TYPE_LABELS } from '@/utils/labels'
import {
  buildCreateRequest,
  buildRecurrenceRequest,
  canBeReceived,
  isRepeatable,
  buildUpdateRequest,
  NOTES_MAX,
  TRANSACTION_FORM_FIELDS,
  usesCard,
  validateTransactionForm,
  type TransactionFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Movimentação em edição; `null` = criação. */
    transaction?: TransactionDetailDto | null
    defaultType?: TransactionType
    defaultAccountId?: string | null
    /** Data local/servidor de "hoje", usada como padrão da data da movimentação. */
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  {
    transaction: null,
    defaultType: 'Expense',
    defaultAccountId: null,
    loading: false,
    errorMessage: null,
    fieldErrors: () => ({}),
  },
)

const emit = defineEmits<{
  submit: [request: CreateTransactionRequest | UpdateTransactionRequest]
  /** Receita nova com "Repetir todo mês" marcado. */
  'submit-recurring': [request: CreateRecurrenceRequest]
  cancel: []
}>()

const categories = useCategoriesStore()
const { errors, clear, setLocal, setServer } = useFormErrors(TRANSACTION_FORM_FIELDS)

const isEdit = computed(() => props.transaction !== null)

function initialValues(): TransactionFormValues {
  const tx = props.transaction
  if (tx) {
    return {
      description: tx.description,
      type: tx.type,
      amount: formatMoneyInput(toCents(tx.amount)),
      categoryId: tx.categoryId,
      accountId: tx.accountId,
      paymentMethod: tx.paymentMethod,
      payWith: tx.creditCardId ? 'card' : 'account',
      creditCardId: tx.creditCardId,
      status: null,
      transactionDate: tx.transactionDate,
      dueDate: tx.dueDate,
      notes: tx.notes ?? '',
      repeatMonthly: false,
      received: false,
      receivedDate: null,
    }
  }
  return {
    description: '',
    type: props.defaultType,
    amount: '',
    categoryId: null,
    accountId: props.defaultAccountId,
    paymentMethod: null,
    payWith: 'account',
    creditCardId: null,
    status: 'Pending',
    transactionDate: props.today,
    dueDate: null,
    notes: '',
    repeatMonthly: false,
    received: false,
    receivedDate: null,
  }
}

const form = reactive<TransactionFormValues>(initialValues())

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const typeOptions = enumOptions(TRANSACTION_TYPE_LABELS)

/** Parcela: valor e data da movimentação são imutáveis (§10, D-026). */
const isInstallment = computed(() => props.transaction?.installment != null)
const withCard = computed(() => usesCard(form))

const categoryOptions = computed(() =>
  buildCategoryOptions(categories.items, form.type, { alwaysInclude: props.transaction?.categoryId }),
)

/** Situação inicial só é editável sem pagamentos e fora de estados finais. */
const canEditStatus = computed(() => {
  const tx = props.transaction
  if (!tx) return true
  return tx.paidAmount === 0 && tx.status !== 'Canceled' && tx.status !== 'Paid' && tx.status !== 'Received'
})

const statusOptions = computed(() => {
  const base: { value: TransactionInitialStatus | null; label: string }[] = [
    { value: 'Planned', label: STATUS_PRESENTATION.Planned.label },
    { value: 'Pending', label: STATUS_PRESENTATION.Pending.label },
  ]
  return isEdit.value ? [{ value: null, label: 'Manter situação atual' }, ...base] : base
})

const minAmountCents = computed(() => toCents(props.transaction?.paidAmount ?? 0))

function onTypeChange(value: TransactionType | null): void {
  if (!value || value === form.type) return
  form.type = value
  if (value !== 'Income') {
    form.repeatMonthly = false
    form.received = false
  }
  // A categoria precisa ser do mesmo tipo da movimentação.
  form.categoryId = null
  clear('categoryId')
  if (value === 'Income') {
    // Cartão só vale para despesas (§10).
    form.payWith = 'account'
    form.creditCardId = null
    if (form.paymentMethod === 'CreditCard') form.paymentMethod = null
    // Receitas não têm situação inicial nem vencimento no formulário.
    if (!isEdit.value) {
      form.status = 'Pending'
      form.dueDate = null
    }
  }
}

/** Ao marcar "Já recebida", sugere a data da movimentação (limitada a hoje). */
function onReceivedToggle(value: boolean): void {
  clear('paymentDate')
  if (value && !form.receivedDate) {
    const date = form.transactionDate ?? props.today
    form.receivedDate = date > props.today ? props.today : date
  }
}

function onSubmit(): void {
  const valid = setLocal(validateTransactionForm(form, { minAmountCents: minAmountCents.value, today: props.today }))
  if (!valid) return
  if (isRepeatable(form, isEdit.value) && form.repeatMonthly) {
    emit('submit-recurring', buildRecurrenceRequest(form))
    return
  }
  emit('submit', isEdit.value ? buildUpdateRequest(form) : buildCreateRequest(form))
}
</script>

<template>
  <form
    class="form-stack"
    novalidate
    :aria-label="isEdit ? 'Editar movimentação' : 'Nova movimentação'"
    @submit.prevent="onSubmit"
  >
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <fieldset v-if="!isEdit" class="tx-form__type">
      <legend>Tipo <span class="tx-form__required" aria-hidden="true">*</span></legend>
      <SelectButton
        :model-value="form.type"
        :options="typeOptions"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        aria-label="Tipo da movimentação"
        @update:model-value="onTypeChange"
      />
    </fieldset>
    <FormField
      v-else
      id="tx-type-readonly"
      v-slot="{ id, describedBy }"
      label="Tipo"
      hint="O tipo não pode ser alterado depois que a movimentação é criada."
    >
      <InputText
        :id="id"
        :model-value="TRANSACTION_TYPE_LABELS[form.type]"
        readonly
        disabled
        name="type"
        :aria-describedby="describedBy"
      />
    </FormField>

    <FormField
      id="tx-description"
      v-slot="{ id, describedBy, invalid }"
      label="Descrição"
      :error="errors.description"
      required
    >
      <InputText
        :id="id"
        v-model="form.description"
        name="description"
        autocomplete="off"
        maxlength="200"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        autofocus
        @input="clear('description')"
      />
    </FormField>

    <div class="form-grid">
      <FormField id="tx-amount" v-slot="{ id, describedBy, invalid }" label="Valor" :error="errors.amount" required>
        <MoneyInput
          :id="id"
          v-model="form.amount"
          :described-by="describedBy"
          :invalid="invalid"
          :disabled="isInstallment"
          @update:model-value="clear('amount')"
        />
      </FormField>

      <FormField
        id="tx-category"
        v-slot="{ id, describedBy, invalid }"
        label="Categoria"
        :error="errors.categoryId"
        required
      >
        <Select
          v-model="form.categoryId"
          :input-id="id"
          :options="categoryOptions"
          option-label="label"
          option-value="value"
          placeholder="Escolha uma categoria"
          filter
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('categoryId')"
        >
          <template #option="{ option }">
            <span class="tx-form__option" :class="{ 'tx-form__option--child': option.depth > 0 }">
              {{ option.depth > 0 ? option.label.split(' › ').pop() : option.label }}
            </span>
          </template>
        </Select>
      </FormField>
    </div>

    <div class="form-grid">
      <FormField
        id="tx-date"
        v-slot="{ id, describedBy, invalid }"
        label="Data da movimentação"
        :error="errors.transactionDate"
        :hint="isInstallment ? 'A data de uma parcela não pode ser alterada.' : undefined"
        required
      >
        <DateInput
          :id="id"
          v-model="form.transactionDate"
          :described-by="describedBy"
          :invalid="invalid"
          :disabled="isInstallment"
          @update:model-value="clear('transactionDate')"
        />
      </FormField>
      <FormField
        v-if="!withCard && form.type === 'Expense'"
        id="tx-due"
        v-slot="{ id, describedBy, invalid }"
        label="Vencimento"
        :error="errors.dueDate"
        hint="Opcional. Movimentações vencidas e não quitadas aparecem como atrasadas."
      >
        <DateInput
          :id="id"
          v-model="form.dueDate"
          :described-by="describedBy"
          :invalid="invalid"
          :min="form.transactionDate"
          @update:model-value="clear('dueDate')"
        />
      </FormField>
    </div>

    <p v-if="isInstallment" class="tx-form__note" data-testid="installment-note">
      <i class="pi pi-info-circle" aria-hidden="true" />
      Esta é a parcela {{ transaction?.installment?.number }}/{{ transaction?.installment?.count }}. O valor e a data
      não podem ser alterados; cancele a parcela ou o plano se necessário.
    </p>

    <PaymentSourceFields
      v-model:pay-with="form.payWith"
      v-model:credit-card-id="form.creditCardId"
      v-model:account-id="form.accountId"
      v-model:payment-method="form.paymentMethod"
      id-prefix="tx"
      :allow-card="form.type === 'Expense'"
      :locked="isEdit"
      :current-account-id="transaction?.accountId ?? null"
      :current-card-id="transaction?.creditCardId ?? null"
      :errors="errors"
      @update:credit-card-id="clear('creditCardId')"
      @update:account-id="clear('accountId')"
      @update:payment-method="clear('paymentMethod')"
    />

    <FormField
      v-if="canEditStatus && form.type === 'Expense'"
      id="tx-status"
      v-slot="{ id, describedBy, invalid }"
      label="Situação"
      :error="errors.status"
      hint="Previsto: ainda não confirmado. Pendente: confirmado e aguardando pagamento."
    >
      <Select
        v-model="form.status"
        :input-id="id"
        :options="statusOptions"
        option-label="label"
        option-value="value"
        :invalid="invalid"
        :aria-describedby="describedBy"
        fluid
        @update:model-value="clear('status')"
      />
    </FormField>

    <div v-if="canBeReceived(form, isEdit) && !form.repeatMonthly" class="tx-form__repeat">
      <Checkbox
        v-model="form.received"
        input-id="tx-received"
        binary
        name="received"
        @update:model-value="onReceivedToggle"
      />
      <label for="tx-received">
        Já recebida
        <small class="tx-form__repeat-hint">
          Para lançamentos retroativos: a receita já nasce como recebida, sem precisar registrar o recebimento depois.
        </small>
      </label>
    </div>
    <FormField
      v-if="canBeReceived(form, isEdit) && form.received"
      id="tx-received-date"
      v-slot="{ id, describedBy, invalid }"
      label="Data do recebimento"
      :error="errors.paymentDate"
      required
    >
      <DateInput
        :id="id"
        v-model="form.receivedDate"
        :described-by="describedBy"
        :invalid="invalid"
        :min="form.transactionDate"
        :max="today"
        @update:model-value="clear('paymentDate')"
      />
    </FormField>

    <div v-if="isRepeatable(form, isEdit) && !form.received" class="tx-form__repeat">
      <Checkbox v-model="form.repeatMonthly" input-id="tx-repeat" binary name="repeatMonthly" />
      <label for="tx-repeat">
        Repetir todo mês
        <small class="tx-form__repeat-hint">
          Para valores fixos (ex.: salário): lança automaticamente todo mês a partir da data acima. Depois você pode
          abrir a movimentação e encerrar a repetição a partir de uma data.
        </small>
      </label>
    </div>

    <FormField
      id="tx-notes"
      v-slot="{ id, describedBy, invalid }"
      label="Observações"
      :error="errors.notes"
      :hint="`${form.notes.length}/${NOTES_MAX} caracteres`"
    >
      <Textarea
        :id="id"
        v-model="form.notes"
        name="notes"
        rows="3"
        auto-resize
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        @input="clear('notes')"
      />
    </FormField>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button
        type="submit"
        :label="isEdit ? 'Salvar alterações' : 'Criar movimentação'"
        icon="pi pi-check"
        :loading="loading"
      />
    </div>
  </form>
</template>

<style scoped>
.tx-form__repeat {
  display: flex;
  gap: 0.625rem;
  align-items: flex-start;
}

.tx-form__repeat-hint {
  display: block;
  margin-top: 0.125rem;
  color: var(--gf-text-muted);
}

.tx-form__type {
  margin: 0;
  padding: 0;
  border: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.tx-form__type legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 500;
  font-size: 0.9375rem;
}

.tx-form__required {
  color: var(--gf-danger);
}

.tx-form__note {
  margin: 0;
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}

.tx-form__option--child {
  padding-left: 1rem;
}
</style>
