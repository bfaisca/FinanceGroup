<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import Textarea from 'primevue/textarea'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import PaymentSourceFields from '@/components/PaymentSourceFields.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { buildCategoryOptions } from '@/modules/categories/tree'
import { useCategoriesStore } from '@/stores/categories'
import type { CreateInstallmentPlanRequest } from '@/types/api'
import {
  buildPlanRequest,
  emptyPlanForm,
  INSTALLMENT_COUNT_MAX,
  INSTALLMENT_COUNT_MIN,
  PLAN_FORM_FIELDS,
  PLAN_NOTES_MAX,
  validatePlanForm,
  type InstallmentPlanFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: CreateInstallmentPlanRequest]; cancel: [] }>()

const categories = useCategoriesStore()
const { errors, clear, setLocal, setServer } = useFormErrors(PLAN_FORM_FIELDS)
const form = reactive<InstallmentPlanFormValues>(emptyPlanForm(props.today))

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

// Parcelas são sempre despesas (§12).
const categoryOptions = computed(() => buildCategoryOptions(categories.items, 'Expense'))

function onSubmit(): void {
  if (!setLocal(validatePlanForm(form))) return
  emit('submit', buildPlanRequest(form))
}
</script>

<template>
  <form class="form-stack" novalidate aria-label="Novo parcelamento" @submit.prevent="onSubmit">
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField
      id="plan-description"
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
      <FormField
        id="plan-total"
        v-slot="{ id, describedBy, invalid }"
        label="Valor total"
        :error="errors.totalAmount"
        required
      >
        <MoneyInput
          :id="id"
          v-model="form.totalAmount"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('totalAmount')"
        />
      </FormField>
      <FormField
        id="plan-count"
        v-slot="{ id, describedBy, invalid }"
        label="Número de parcelas"
        :error="errors.installmentCount"
        :hint="`De ${INSTALLMENT_COUNT_MIN} a ${INSTALLMENT_COUNT_MAX}.`"
        required
      >
        <InputNumber
          v-model="form.installmentCount"
          :input-id="id"
          :min="INSTALLMENT_COUNT_MIN"
          :max="INSTALLMENT_COUNT_MAX"
          :use-grouping="false"
          :max-fraction-digits="0"
          inputmode="numeric"
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('installmentCount')"
        />
      </FormField>
    </div>

    <div class="form-grid">
      <FormField
        id="plan-first-date"
        v-slot="{ id, describedBy, invalid }"
        label="Data da 1ª parcela"
        :error="errors.firstDate"
        hint="Com cartão, é a data da compra. As datas das demais parcelas e o vencimento são definidos pelo sistema."
        required
      >
        <DateInput
          :id="id"
          v-model="form.firstDate"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('firstDate')"
        />
      </FormField>
      <FormField
        id="plan-category"
        v-slot="{ id, describedBy, invalid }"
        label="Categoria"
        :error="errors.categoryId"
        hint="Somente categorias de despesa."
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

    <PaymentSourceFields
      v-model:pay-with="form.payWith"
      v-model:credit-card-id="form.creditCardId"
      v-model:account-id="form.accountId"
      v-model:payment-method="form.paymentMethod"
      id-prefix="plan"
      :errors="errors"
      @update:credit-card-id="clear('creditCardId')"
      @update:account-id="clear('accountId')"
      @update:payment-method="clear('paymentMethod')"
    />

    <FormField
      id="plan-notes"
      v-slot="{ id, describedBy, invalid }"
      label="Observações"
      :error="errors.notes"
      :hint="`${form.notes.length}/${PLAN_NOTES_MAX} caracteres`"
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
      <Button type="submit" label="Criar parcelamento" icon="pi pi-check" :loading="loading" />
    </div>
  </form>
</template>
