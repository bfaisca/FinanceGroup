<script setup lang="ts">
import { reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Select from 'primevue/select'
import DateInput from '@/components/DateInput.vue'
import FormField from '@/components/FormField.vue'
import MoneyInput from '@/components/MoneyInput.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import type { AccountDto, CreateAccountRequest, UpdateAccountRequest } from '@/types/api'
import { ACCOUNT_TYPE_LABELS, enumOptions } from '@/utils/labels'
import {
  ACCOUNT_FORM_FIELDS,
  buildCreateAccountRequest,
  buildUpdateAccountRequest,
  validateAccountForm,
  type AccountFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Conta em edição; `null` = criação. */
    account?: AccountDto | null
    today: string
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
  }>(),
  { account: null, loading: false, errorMessage: null, fieldErrors: () => ({}) },
)

const emit = defineEmits<{ submit: [request: CreateAccountRequest | UpdateAccountRequest]; cancel: [] }>()

const isEdit = props.account !== null
const { errors, clear, setLocal, setServer } = useFormErrors(ACCOUNT_FORM_FIELDS)

const form = reactive<AccountFormValues>({
  name: props.account?.name ?? '',
  institution: props.account?.institution ?? '',
  type: props.account?.type ?? 'Checking',
  initialBalance: '',
  balanceReferenceDate: null,
})

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

const typeOptions = enumOptions(ACCOUNT_TYPE_LABELS)

function onSubmit(): void {
  const valid = setLocal(validateAccountForm(form, { today: props.today, creating: !isEdit }))
  if (!valid) return
  emit('submit', isEdit ? buildUpdateAccountRequest(form) : buildCreateAccountRequest(form))
}
</script>

<template>
  <form class="form-stack" novalidate :aria-label="isEdit ? 'Editar conta' : 'Nova conta'" @submit.prevent="onSubmit">
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField id="acc-name" v-slot="{ id, describedBy, invalid }" label="Nome" :error="errors.name" required>
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
      id="acc-institution"
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

    <FormField id="acc-type" v-slot="{ id, describedBy, invalid }" label="Tipo" :error="errors.type" required>
      <Select
        v-model="form.type"
        :input-id="id"
        :options="typeOptions"
        option-label="label"
        option-value="value"
        :invalid="invalid"
        :aria-describedby="describedBy"
        fluid
        @update:model-value="clear('type')"
      />
    </FormField>

    <template v-if="!isEdit">
      <FormField
        id="acc-balance"
        v-slot="{ id, describedBy, invalid }"
        label="Saldo inicial"
        :error="errors.initialBalance"
        hint="Opcional. Informe o saldo atual da conta para ver o saldo projetado; pode ser negativo."
      >
        <MoneyInput
          :id="id"
          v-model="form.initialBalance"
          allow-negative
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('initialBalance')"
        />
      </FormField>
      <FormField
        id="acc-balance-date"
        v-slot="{ id, describedBy, invalid }"
        label="Data do saldo"
        :error="errors.balanceReferenceDate"
        hint="Se vazia, usa a data de hoje. Não pode ser futura."
      >
        <DateInput
          :id="id"
          v-model="form.balanceReferenceDate"
          :max="today"
          :described-by="describedBy"
          :invalid="invalid"
          @update:model-value="clear('balanceReferenceDate')"
        />
      </FormField>
    </template>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button
        type="submit"
        :label="isEdit ? 'Salvar alterações' : 'Criar conta'"
        icon="pi pi-check"
        :loading="loading"
      />
    </div>
  </form>
</template>
