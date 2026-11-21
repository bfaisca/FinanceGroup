<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import ToggleSwitch from 'primevue/toggleswitch'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { buildCategoryOptions } from '@/modules/categories/tree'
import { NOTES_MAX } from '@/modules/transactions/validation'
import { useAccountsStore } from '@/stores/accounts'
import { useCategoriesStore } from '@/stores/categories'
import type { TransactionInitialStatus } from '@/types/api'
import { enumOptions, PAYMENT_METHOD_LABELS, STATUS_PRESENTATION } from '@/utils/labels'
import { formatMoneyInput, normalizeMoneyText, parseMoneyToCents, toCents } from '@/utils/money'
import {
  DEFAULT_RECURRENCE_FREQUENCY,
  RECURRENCE_FREQUENCY_LABELS,
  RECURRENCE_MAX,
  RECURRENCE_MIN,
  recurrencePreview,
  seriesBadge,
} from '../recurrence'
import type { CreateGroupTransactionRequest, GroupTransactionDetailDto, UpdateGroupTransactionRequest } from '../types'
import {
  buildGroupCreateRequest,
  buildGroupUpdateRequest,
  GROUP_TRANSACTION_FORM_FIELDS,
  mapGroupServerErrors,
  validateGroupTransactionForm,
  type GroupTransactionFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Movimentação em edição; `null` = criação. */
    transaction?: GroupTransactionDetailDto | null
    /** "Hoje" da aplicação: data padrão da movimentação. */
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { transaction: null, loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{
  submit: [request: CreateGroupTransactionRequest | UpdateGroupTransactionRequest]
  cancel: []
}>()

const categories = useCategoriesStore()
const accounts = useAccountsStore()
const { errors, clear, setLocal, setServer } = useFormErrors(GROUP_TRANSACTION_FORM_FIELDS)

const isEdit = computed(() => props.transaction !== null)

function initialValues(): GroupTransactionFormValues {
  const tx = props.transaction
  if (tx) {
    return {
      description: tx.description,
      amount: formatMoneyInput(toCents(tx.amount)),
      categoryId: tx.categoryId,
      paymentMethod: tx.paymentMethod,
      status: null,
      transactionDate: tx.transactionDate,
      dueDate: tx.dueDate,
      notes: tx.notes ?? '',
      ...creationDefaults(),
    }
  }
  return {
    description: '',
    amount: '',
    categoryId: null,
    paymentMethod: null,
    status: 'Pending',
    transactionDate: props.today,
    dueDate: null,
    notes: '',
    ...creationDefaults(),
  }
}

function creationDefaults() {
  return {
    paid: false,
    paymentDate: props.today,
    paymentAmount: '',
    paymentInterest: '',
    paymentAccountId: null,
    paymentMethodPaid: null,
    repeat: false,
    frequency: DEFAULT_RECURRENCE_FREQUENCY,
    occurrences: '10',
  }
}

const form = reactive<GroupTransactionFormValues>(initialValues())

watch(
  () => props.fieldErrors,
  (value) => setServer(mapGroupServerErrors(value)),
  { deep: true },
)

onMounted(() => {
  // Contas só são necessárias para o pagamento na criação (a conta é privada e opcional).
  if (!isEdit.value) void accounts.load()
})

const methodOptions = enumOptions(PAYMENT_METHOD_LABELS)
const accountOptions = computed(() => accounts.activeItems.map((a) => ({ value: a.id, label: a.name })))
const frequencyOptions = enumOptions(RECURRENCE_FREQUENCY_LABELS)

// Grupos só têm despesas (D-046): só categorias de despesa.
const categoryOptions = computed(() =>
  buildCategoryOptions(categories.items, 'Expense', { alwaysInclude: props.transaction?.categoryId }),
)

// ---- "Já foi paga": o valor pago acompanha o valor da conta enquanto o usuário não o altera ----
const paymentAmountTouched = ref(false)

watch(
  () => form.amount,
  (value) => {
    if (form.paid && !paymentAmountTouched.value) form.paymentAmount = normalizeMoneyText(value)
  },
)

function onPaidToggle(value: boolean): void {
  form.paid = value
  if (value) {
    if (!form.paymentDate) form.paymentDate = props.today
    if (!paymentAmountTouched.value) form.paymentAmount = normalizeMoneyText(form.amount)
  } else {
    clear('paymentDate')
    clear('paymentAmount')
    clear('paymentInterest')
  }
}

function onPaymentAmountInput(): void {
  paymentAmountTouched.value = true
  clear('paymentAmount')
}

// ---- "Repetir": prévia visual (o backend calcula as datas de verdade) ----
const preview = computed(() =>
  form.repeat
    ? recurrencePreview(
        form.transactionDate,
        form.frequency,
        /^\d+$/.test(form.occurrences.trim()) ? Number(form.occurrences.trim()) : Number.NaN,
        parseMoneyToCents(form.amount),
      )
    : null,
)

/** Situação inicial só é editável sem pagamentos e fora de estados finais. */
const canEditStatus = computed(() => {
  const tx = props.transaction
  if (!tx) return !form.paid
  return tx.paidAmount === 0 && tx.status !== 'Canceled' && tx.status !== 'Paid' && tx.status !== 'Received'
})

const statusOptions = computed(() => {
  const base: { value: TransactionInitialStatus | null; label: string }[] = [
    { value: 'Planned', label: STATUS_PRESENTATION.Planned.label },
    { value: 'Pending', label: STATUS_PRESENTATION.Pending.label },
  ]
  return isEdit.value ? [{ value: null, label: 'Manter situação atual' }, ...base] : base
})

function onSubmit(): void {
  const valid = setLocal(
    validateGroupTransactionForm(form, {
      minAmountCents: toCents(props.transaction?.paidAmount ?? 0),
      today: props.today,
      creating: !isEdit.value,
    }),
  )
  if (!valid) return
  emit('submit', isEdit.value ? buildGroupUpdateRequest(form) : buildGroupCreateRequest(form))
}
</script>

<template>
  <form
    class="form-stack"
    novalidate
    :aria-label="isEdit ? 'Editar movimentação do grupo' : 'Nova movimentação do grupo'"
    @submit.prevent="onSubmit"
  >
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <Message :pt="NOTE_PT" severity="secondary" :closable="false" class="gtx-form__privacy" data-testid="privacy-note">
      Esta movimentação fica visível para todos os participantes do grupo. Contas e cartões pessoais não fazem parte
      dela: cada pessoa informa a própria conta somente ao registrar o seu pagamento.
    </Message>

    <Message
      v-if="isEdit"
      :pt="NOTE_PT"
      severity="info"
      :closable="false"
      class="gtx-form__single"
      data-testid="edit-single-note"
    >
      <i class="pi pi-info-circle" aria-hidden="true" /> Edita apenas esta conta<template v-if="transaction?.series">
        (ocorrência {{ seriesBadge(transaction.series) }} da série)</template
      >. As demais ocorrências não são alteradas.
    </Message>

    <FormField
      id="gtx-description"
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
        @input="clear('description')"
      />
    </FormField>

    <div class="form-grid">
      <FormField id="gtx-amount" v-slot="{ id, describedBy, invalid }" label="Valor" :error="errors.amount" required>
        <MoneyInput
          :id="id"
          v-model="form.amount"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('amount')"
        />
      </FormField>

      <FormField
        id="gtx-category"
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
        />
      </FormField>
    </div>

    <div class="form-grid">
      <FormField
        id="gtx-date"
        v-slot="{ id, describedBy, invalid }"
        label="Data da movimentação"
        :error="errors.transactionDate"
        required
      >
        <DateInput
          :id="id"
          v-model="form.transactionDate"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('transactionDate')"
        />
      </FormField>
      <FormField
        id="gtx-due"
        v-slot="{ id, describedBy, invalid }"
        label="Vencimento"
        :error="errors.dueDate"
        hint="Opcional. Pode ser qualquer data, inclusive já vencida. Contas vencidas e não quitadas aparecem como atrasadas."
      >
        <DateInput
          :id="id"
          v-model="form.dueDate"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('dueDate')"
        />
      </FormField>
    </div>

    <FormField
      id="gtx-method"
      v-slot="{ id, describedBy, invalid }"
      label="Método de pagamento"
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

    <FormField
      v-if="canEditStatus"
      id="gtx-status"
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
        placeholder="Escolha a situação"
        :invalid="invalid"
        :aria-describedby="describedBy"
        fluid
        @update:model-value="clear('status')"
      />
    </FormField>

    <template v-if="!isEdit">
      <fieldset class="gtx-form__section" data-testid="paid-section">
        <legend>Pagamento</legend>
        <div class="gtx-form__toggle">
          <ToggleSwitch
            input-id="gtx-paid"
            :model-value="form.paid"
            aria-describedby="gtx-paid-hint"
            @update:model-value="onPaidToggle"
          />
          <label for="gtx-paid">Já foi paga</label>
        </div>
        <small id="gtx-paid-hint" class="gtx-form__hint">
          Registra a conta e o seu pagamento de uma vez (pode ser de qualquer data passada).
        </small>

        <div v-if="form.paid" class="form-stack" data-testid="paid-fields">
          <div class="form-grid">
            <FormField
              id="gtx-pay-date"
              v-slot="{ id, describedBy, invalid }"
              label="Data do pagamento"
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
            <FormField
              id="gtx-pay-amount"
              v-slot="{ id, describedBy, invalid }"
              label="Valor pago (da conta)"
              :error="errors.paymentAmount"
              hint="Padrão: o valor da conta. Sem os juros/multa."
            >
              <MoneyInput
                :id="id"
                v-model="form.paymentAmount"
                :described-by="describedBy"
                :invalid="invalid"
                @update:model-value="onPaymentAmountInput"
              />
            </FormField>
          </div>
          <FormField
            id="gtx-pay-interest"
            v-slot="{ id, describedBy, invalid }"
            label="Juros/multa"
            :error="errors.paymentInterest"
            hint="Opcional. Pagou atrasado com juros? Informe o valor da conta e os juros separados."
          >
            <MoneyInput
              :id="id"
              v-model="form.paymentInterest"
              :described-by="describedBy"
              :invalid="invalid"
              @update:model-value="clear('paymentInterest')"
            />
          </FormField>
          <div class="form-grid">
            <FormField
              id="gtx-pay-method"
              v-slot="{ id, describedBy, invalid }"
              label="Método do pagamento"
              :error="errors.paymentMethodPaid"
              hint="Opcional. Visível para o grupo."
            >
              <Select
                v-model="form.paymentMethodPaid"
                :input-id="id"
                :options="methodOptions"
                option-label="label"
                option-value="value"
                placeholder="Não informado"
                show-clear
                :invalid="invalid"
                :aria-describedby="describedBy"
                fluid
                @update:model-value="clear('paymentMethodPaid')"
              />
            </FormField>
            <FormField
              id="gtx-pay-account"
              v-slot="{ id, describedBy, invalid }"
              label="Sua conta (privada)"
              :error="errors.paymentAccountId"
              hint="Opcional. Sua conta é privada: o grupo não a vê. Ela só afeta o seu saldo individual."
            >
              <Select
                v-model="form.paymentAccountId"
                :input-id="id"
                :options="accountOptions"
                option-label="label"
                option-value="value"
                placeholder="Não vincular a uma conta"
                show-clear
                :invalid="invalid"
                :aria-describedby="describedBy"
                fluid
                @update:model-value="clear('paymentAccountId')"
              />
            </FormField>
          </div>
        </div>
      </fieldset>

      <fieldset class="gtx-form__section" data-testid="repeat-section">
        <legend>Repetir</legend>
        <div class="gtx-form__toggle">
          <ToggleSwitch
            input-id="gtx-repeat"
            :model-value="form.repeat"
            aria-describedby="gtx-repeat-hint"
            @update:model-value="form.repeat = $event"
          />
          <label for="gtx-repeat">Repetir esta conta</label>
        </div>
        <small id="gtx-repeat-hint" class="gtx-form__hint">
          Cria várias contas de uma vez (ex.: IPTU em 10 vezes). Depois de criadas, cada uma é editada, paga ou
          cancelada separadamente.
        </small>

        <div v-if="form.repeat" class="form-stack" data-testid="repeat-fields">
          <div class="form-grid">
            <FormField
              id="gtx-frequency"
              v-slot="{ id, describedBy, invalid }"
              label="Frequência"
              :error="errors.frequency"
              required
            >
              <Select
                v-model="form.frequency"
                :input-id="id"
                :options="frequencyOptions"
                option-label="label"
                option-value="value"
                placeholder="Escolha a frequência"
                :invalid="invalid"
                :aria-describedby="describedBy"
                fluid
                @update:model-value="clear('frequency')"
              />
            </FormField>
            <FormField
              id="gtx-occurrences"
              v-slot="{ id, describedBy, invalid }"
              label="Quantidade de vezes"
              :error="errors.occurrences"
              :hint="`De ${RECURRENCE_MIN} a ${RECURRENCE_MAX}, contando a primeira.`"
              required
            >
              <InputText
                :id="id"
                v-model="form.occurrences"
                name="occurrences"
                inputmode="numeric"
                autocomplete="off"
                :aria-describedby="describedBy"
                :aria-invalid="invalid"
                :invalid="invalid"
                fluid
                @input="clear('occurrences')"
              />
            </FormField>
          </div>
          <Message v-if="preview" :pt="NOTE_PT" severity="secondary" :closable="false" data-testid="repeat-preview">
            <i class="pi pi-calendar" aria-hidden="true" /> {{ preview.summary }}. As datas finais são calculadas pelo
            sistema.
          </Message>
          <Message v-if="form.paid" :pt="NOTE_PT" severity="warn" :closable="false" data-testid="repeat-paid-warning">
            O pagamento informado vale apenas para a 1ª ocorrência; as demais ficam em aberto.
          </Message>
        </div>
      </fieldset>
    </template>

    <FormField
      id="gtx-notes"
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
.gtx-form__section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 0;
  margin: 0;
  padding: 0.75rem 1rem 1rem;
  border: 1px solid var(--gf-border);
  border-radius: var(--gf-radius);
}

.gtx-form__section legend {
  padding: 0 0.375rem;
  font-weight: 600;
  font-size: 0.9375rem;
}

.gtx-form__toggle {
  display: flex;
  gap: 0.625rem;
  align-items: center;
}

.gtx-form__toggle label {
  font-weight: 500;
  cursor: pointer;
}

.gtx-form__hint {
  color: var(--gf-text-muted);
}
</style>
