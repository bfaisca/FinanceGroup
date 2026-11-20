<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { useAccountsStore } from '@/stores/accounts'
import type { CreditCardDto, CreditCardRequest } from '@/types/api'
import {
  buildCreditCardRequest,
  CREDIT_CARD_FORM_FIELDS,
  DAY_MAX,
  DAY_MIN,
  emptyCreditCardForm,
  formValuesFromCard,
  validateCreditCardForm,
  type CreditCardFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Cartão em edição; `null` = criação. */
    card?: CreditCardDto | null
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { card: null, loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: CreditCardRequest]; cancel: [] }>()

const accounts = useAccountsStore()
const { errors, clear, setLocal, setServer } = useFormErrors(CREDIT_CARD_FORM_FIELDS)
const isEdit = props.card !== null
const form = reactive<CreditCardFormValues>(props.card ? formValuesFromCard(props.card) : emptyCreditCardForm())

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

/** Apenas contas ativas — mantém a associada atual visível (mesmo inativa) para não perder o vínculo. */
const accountOptions = computed(() =>
  accounts.items
    .filter((a) => a.isActive || a.id === props.card?.accountId)
    .map((a) => ({ value: a.id, label: a.isActive ? a.name : `${a.name} (inativa)` })),
)

function onSubmit(): void {
  if (!setLocal(validateCreditCardForm(form))) return
  emit('submit', buildCreditCardRequest(form))
}
</script>

<template>
  <form
    class="form-stack"
    novalidate
    :aria-label="isEdit ? 'Editar cartão de crédito' : 'Novo cartão de crédito'"
    @submit.prevent="onSubmit"
  >
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField id="card-name" v-slot="{ id, describedBy, invalid }" label="Nome" :error="errors.name" required>
      <InputText
        :id="id"
        v-model="form.name"
        name="name"
        maxlength="100"
        autocomplete="off"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        autofocus
        fluid
        @input="clear('name')"
      />
    </FormField>

    <FormField
      id="card-institution"
      v-slot="{ id, describedBy, invalid }"
      label="Instituição"
      :error="errors.institution"
      hint="Opcional."
    >
      <InputText
        :id="id"
        v-model="form.institution"
        name="institution"
        maxlength="100"
        autocomplete="off"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        @input="clear('institution')"
      />
    </FormField>

    <FormField
      id="card-limit"
      v-slot="{ id, describedBy, invalid }"
      label="Limite"
      :error="errors.creditLimit"
      required
    >
      <MoneyInput
        :id="id"
        v-model="form.creditLimit"
        :described-by="describedBy"
        :invalid="invalid"
        @update:model-value="clear('creditLimit')"
      />
    </FormField>

    <div class="form-grid">
      <FormField
        id="card-closing"
        v-slot="{ id, describedBy, invalid }"
        label="Dia de fechamento"
        :error="errors.closingDay"
        :hint="`De ${DAY_MIN} a ${DAY_MAX}. Em meses mais curtos, vale o último dia.`"
        required
      >
        <InputNumber
          v-model="form.closingDay"
          :input-id="id"
          :min="DAY_MIN"
          :max="DAY_MAX"
          :use-grouping="false"
          :max-fraction-digits="0"
          inputmode="numeric"
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('closingDay')"
        />
      </FormField>
      <FormField
        id="card-due"
        v-slot="{ id, describedBy, invalid }"
        label="Dia de vencimento"
        :error="errors.dueDay"
        :hint="`De ${DAY_MIN} a ${DAY_MAX}.`"
        required
      >
        <InputNumber
          v-model="form.dueDay"
          :input-id="id"
          :min="DAY_MIN"
          :max="DAY_MAX"
          :use-grouping="false"
          :max-fraction-digits="0"
          inputmode="numeric"
          :invalid="invalid"
          :aria-describedby="describedBy"
          fluid
          @update:model-value="clear('dueDay')"
        />
      </FormField>
    </div>

    <FormField
      id="card-account"
      v-slot="{ id, describedBy, invalid }"
      label="Conta associada"
      :error="errors.accountId"
      hint="Opcional. As compras no cartão entram no saldo projetado desta conta, na data de vencimento da fatura."
    >
      <Select
        v-model="form.accountId"
        :input-id="id"
        :options="accountOptions"
        option-label="label"
        option-value="value"
        placeholder="Sem conta associada"
        show-clear
        :invalid="invalid"
        :aria-describedby="describedBy"
        fluid
        @update:model-value="clear('accountId')"
      />
    </FormField>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button
        type="submit"
        :label="isEdit ? 'Salvar alterações' : 'Criar cartão'"
        icon="pi pi-check"
        :loading="loading"
      />
    </div>
  </form>
</template>
