<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
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
import type { CreateRecurrenceRequest, RecurrenceDto, TransactionType, UpdateRecurrenceRequest } from '@/types/api'
import { formatDate } from '@/utils/format'
import { enumOptions, FREQUENCY_OPTION_LABELS, TRANSACTION_TYPE_LABELS } from '@/utils/labels'
import { frequencyLabel } from '@/utils/recurrence'
import {
  buildCreateRecurrenceRequest,
  buildUpdateRecurrenceRequest,
  emptyRecurrenceForm,
  formValuesFromRecurrence,
  INTERVAL_MAX,
  INTERVAL_MIN,
  RECURRENCE_FORM_FIELDS,
  RECURRENCE_NOTES_MAX,
  validateRecurrenceForm,
  type RecurrenceFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Regra em edição; `null` = criação. */
    recurrence?: RecurrenceDto | null
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { recurrence: null, loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: CreateRecurrenceRequest | UpdateRecurrenceRequest]; cancel: [] }>()

const categories = useCategoriesStore()
const { errors, clear, setLocal, setServer } = useFormErrors(RECURRENCE_FORM_FIELDS)
const isEdit = props.recurrence !== null
const form = reactive<RecurrenceFormValues>(
  props.recurrence ? formValuesFromRecurrence(props.recurrence) : emptyRecurrenceForm(props.today),
)

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const typeOptions = enumOptions(TRANSACTION_TYPE_LABELS)
const frequencyOptions = enumOptions(FREQUENCY_OPTION_LABELS)
const isCustom = computed(() => form.frequency === 'Custom')
const categoryOptions = computed(() =>
  buildCategoryOptions(categories.items, form.type, { alwaysInclude: props.recurrence?.categoryId }),
)
const preview = computed(() => (form.interval ? frequencyLabel(form.frequency, form.interval) : null))

function onTypeChange(value: TransactionType | null): void {
  if (!value || value === form.type) return
  form.type = value
  form.categoryId = null
  clear('categoryId')
  if (value === 'Income') {
    form.payWith = 'account'
    form.creditCardId = null
    if (form.paymentMethod === 'CreditCard') form.paymentMethod = null
  }
}

function onSubmit(): void {
  if (!setLocal(validateRecurrenceForm(form, { editing: isEdit }))) return
  emit('submit', isEdit ? buildUpdateRecurrenceRequest(form) : buildCreateRecurrenceRequest(form))
}
</script>

<template>
  <form
    class="form-stack"
    novalidate
    :aria-label="isEdit ? 'Editar recorrência' : 'Nova recorrência'"
    @submit.prevent="onSubmit"
  >
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <fieldset v-if="!isEdit" class="rec-form__type">
      <legend>Tipo <span class="rec-form__required" aria-hidden="true">*</span></legend>
      <SelectButton
        :model-value="form.type"
        :options="typeOptions"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        aria-label="Tipo da recorrência"
        @update:model-value="onTypeChange"
      />
    </fieldset>
    <FormField
      v-else
      id="rec-type-readonly"
      v-slot="{ id, describedBy }"
      label="Tipo"
      hint="O tipo não pode ser alterado depois que a recorrência é criada."
    >
      <InputText
        :id="id"
        :model-value="TRANSACTION_TYPE_LABELS[form.type]"
        readonly
        disabled
        :aria-describedby="describedBy"
        fluid
      />
    </FormField>

    <FormField
      id="rec-description"
      v-slot="{ id, describedBy, invalid }"
      label="Descrição"
      :error="errors.description"
      required
    >
      <InputText
        :id="id"
        v-model="form.description"
        name="description"
        maxlength="200"
        autocomplete="off"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        autofocus
        fluid
        @input="clear('description')"
      />
    </FormField>

    <div class="form-grid">
      <FormField id="rec-amount" v-slot="{ id, describedBy, invalid }" label="Valor" :error="errors.amount" required>
        <MoneyInput
          :id="id"
          v-model="form.amount"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('amount')"
        />
      </FormField>
      <FormField
        id="rec-category"
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
        id="rec-frequency"
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
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('frequency')"
        />
      </FormField>
      <FormField
        id="rec-interval"
        v-slot="{ id, describedBy, invalid }"
        :label="isCustom ? 'A cada quantos dias' : 'Intervalo (repetições do período)'"
        :error="errors.interval"
        :hint="
          isCustom
            ? `Número de dias entre ocorrências (${INTERVAL_MIN} a ${INTERVAL_MAX}).`
            : `Use 1 para o período normal. Ex.: Mensal com 2 = a cada 2 meses (${INTERVAL_MIN} a ${INTERVAL_MAX}).`
        "
        required
      >
        <InputNumber
          v-model="form.interval"
          :input-id="id"
          :min="INTERVAL_MIN"
          :max="INTERVAL_MAX"
          :use-grouping="false"
          :max-fraction-digits="0"
          :suffix="isCustom ? ' dias' : undefined"
          inputmode="numeric"
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('interval')"
        />
      </FormField>
    </div>
    <p v-if="preview" class="rec-form__preview" data-testid="frequency-preview" aria-live="polite">
      <i class="pi pi-sync" aria-hidden="true" /> Repetição: {{ preview }}
    </p>

    <div class="form-grid">
      <FormField
        v-if="!isEdit"
        id="rec-start"
        v-slot="{ id, describedBy, invalid }"
        label="Início"
        :error="errors.startDate"
        hint="Define a primeira ocorrência."
        required
      >
        <DateInput
          :id="id"
          v-model="form.startDate"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('startDate')"
        />
      </FormField>
      <FormField
        v-else
        id="rec-start-readonly"
        v-slot="{ id, describedBy }"
        label="Início"
        hint="A data de início não pode ser alterada."
      >
        <InputText
          :id="id"
          :model-value="formatDate(form.startDate)"
          readonly
          disabled
          :aria-describedby="describedBy"
          fluid
        />
      </FormField>
      <FormField
        id="rec-end"
        v-slot="{ id, describedBy, invalid }"
        label="Término"
        :error="errors.endDate"
        hint="Opcional. Sem término, a regra continua gerando ocorrências."
      >
        <DateInput
          :id="id"
          v-model="form.endDate"
          :min="form.startDate"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('endDate')"
        />
      </FormField>
    </div>

    <PaymentSourceFields
      v-model:pay-with="form.payWith"
      v-model:credit-card-id="form.creditCardId"
      v-model:account-id="form.accountId"
      v-model:payment-method="form.paymentMethod"
      id-prefix="rec"
      :allow-card="form.type === 'Expense'"
      :current-account-id="recurrence?.accountId ?? null"
      :current-card-id="recurrence?.creditCardId ?? null"
      :errors="errors"
      @update:credit-card-id="clear('creditCardId')"
      @update:account-id="clear('accountId')"
      @update:payment-method="clear('paymentMethod')"
    />

    <FormField
      id="rec-notes"
      v-slot="{ id, describedBy, invalid }"
      label="Observações"
      :error="errors.notes"
      :hint="`${form.notes.length}/${RECURRENCE_NOTES_MAX} caracteres`"
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

    <p v-if="isEdit" class="rec-form__note">
      <i class="pi pi-info-circle" aria-hidden="true" />
      Ao salvar, as ocorrências futuras sem pagamento e sem edição manual serão regeneradas conforme a nova regra.
      Ocorrências pagas, vencidas, canceladas ou editadas permanecem como estão.
    </p>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button
        type="submit"
        :label="isEdit ? 'Salvar alterações' : 'Criar recorrência'"
        icon="pi pi-check"
        :loading="loading"
      />
    </div>
  </form>
</template>

<style scoped>
.rec-form__type {
  margin: 0;
  padding: 0;
  border: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.rec-form__type legend {
  padding: 0;
  margin-bottom: 0.375rem;
  font-weight: 500;
  font-size: 0.9375rem;
}

.rec-form__required {
  color: var(--gf-danger);
}

.rec-form__preview,
.rec-form__note {
  margin: 0;
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
  color: var(--gf-text-muted);
  font-size: 0.875rem;
}
</style>
