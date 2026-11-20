<script setup lang="ts">
import { NOTE_PT } from '../notice'
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import Textarea from 'primevue/textarea'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { buildCategoryOptions } from '@/modules/categories/tree'
import { NOTES_MAX } from '@/modules/transactions/validation'
import { useCategoriesStore } from '@/stores/categories'
import type { TransactionInitialStatus, TransactionType } from '@/types/api'
import { enumOptions, PAYMENT_METHOD_LABELS, STATUS_PRESENTATION, TRANSACTION_TYPE_LABELS } from '@/utils/labels'
import { formatMoneyInput, toCents } from '@/utils/money'
import type { CreateGroupTransactionRequest, GroupTransactionDetailDto, UpdateGroupTransactionRequest } from '../types'
import {
  buildGroupCreateRequest,
  buildGroupUpdateRequest,
  GROUP_TRANSACTION_FORM_FIELDS,
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
const { errors, clear, setLocal, setServer } = useFormErrors(GROUP_TRANSACTION_FORM_FIELDS)

const isEdit = computed(() => props.transaction !== null)

function initialValues(): GroupTransactionFormValues {
  const tx = props.transaction
  if (tx) {
    return {
      description: tx.description,
      type: tx.type,
      amount: formatMoneyInput(toCents(tx.amount)),
      categoryId: tx.categoryId,
      paymentMethod: tx.paymentMethod,
      status: null,
      transactionDate: tx.transactionDate,
      dueDate: tx.dueDate,
      notes: tx.notes ?? '',
    }
  }
  return {
    description: '',
    type: 'Expense',
    amount: '',
    categoryId: null,
    paymentMethod: null,
    status: 'Pending',
    transactionDate: props.today,
    dueDate: null,
    notes: '',
  }
}

const form = reactive<GroupTransactionFormValues>(initialValues())

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const typeOptions = enumOptions(TRANSACTION_TYPE_LABELS)
const methodOptions = enumOptions(PAYMENT_METHOD_LABELS)

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

function onTypeChange(value: TransactionType | null): void {
  if (!value || value === form.type) return
  form.type = value
  // A categoria precisa ser do mesmo tipo da movimentação.
  form.categoryId = null
  clear('categoryId')
}

function onSubmit(): void {
  const valid = setLocal(
    validateGroupTransactionForm(form, { minAmountCents: toCents(props.transaction?.paidAmount ?? 0) }),
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

    <fieldset v-if="!isEdit" class="gtx-form__type">
      <legend>Tipo <span class="gtx-form__required" aria-hidden="true">*</span></legend>
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
      id="gtx-type-readonly"
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
.gtx-form__type {
  margin: 0;
  padding: 0;
  border: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.gtx-form__type legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 500;
  font-size: 0.9375rem;
}

.gtx-form__required {
  color: var(--gf-danger);
}
</style>
