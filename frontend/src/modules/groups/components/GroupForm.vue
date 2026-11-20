<script setup lang="ts">
import { reactive, watch } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import Textarea from 'primevue/textarea'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import type { GroupRequest } from '../types'
import {
  buildGroupRequest,
  GROUP_DESCRIPTION_MAX,
  GROUP_FORM_FIELDS,
  GROUP_NAME_MAX,
  validateGroupForm,
  type GroupFormValues,
} from '../validation'

const props = withDefaults(
  defineProps<{
    /** Valores iniciais (edição); vazio = criação. */
    initial?: { name: string; description: string | null } | null
    loading?: boolean
    errorMessage?: string | null
    fieldErrors?: Record<string, string[]>
    submitLabel?: string
    /** Prefixo dos ids (evita colisão se houver dois formulários na página). */
    idPrefix?: string
  }>(),
  {
    initial: null,
    loading: false,
    errorMessage: null,
    fieldErrors: () => ({}),
    submitLabel: 'Salvar',
    idPrefix: 'group',
  },
)

const emit = defineEmits<{ submit: [request: GroupRequest]; cancel: [] }>()

const { errors, clear, setLocal, setServer } = useFormErrors(GROUP_FORM_FIELDS)

const form = reactive<GroupFormValues>({
  name: props.initial?.name ?? '',
  description: props.initial?.description ?? '',
})

watch(
  () => props.fieldErrors,
  (value) => setServer(value),
  { deep: true },
)

function onSubmit(): void {
  if (!setLocal(validateGroupForm(form))) return
  emit('submit', buildGroupRequest(form))
}
</script>

<template>
  <form class="form-stack" novalidate aria-label="Dados do grupo" @submit.prevent="onSubmit">
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="form-error">{{ errorMessage }}</Message>

    <FormField
      :id="`${idPrefix}-name`"
      v-slot="{ id, describedBy, invalid }"
      label="Nome do grupo"
      :error="errors.name"
      required
    >
      <InputText
        :id="id"
        v-model="form.name"
        name="name"
        autocomplete="off"
        :maxlength="GROUP_NAME_MAX"
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        @input="clear('name')"
      />
    </FormField>

    <FormField
      :id="`${idPrefix}-description`"
      v-slot="{ id, describedBy, invalid }"
      label="Descrição"
      :error="errors.description"
      :hint="`Opcional. ${form.description.length}/${GROUP_DESCRIPTION_MAX} caracteres`"
    >
      <Textarea
        :id="id"
        v-model="form.description"
        name="description"
        rows="3"
        auto-resize
        :aria-describedby="describedBy"
        :aria-invalid="invalid"
        :invalid="invalid"
        fluid
        @input="clear('description')"
      />
    </FormField>

    <div class="form-actions">
      <Button type="button" label="Cancelar" severity="secondary" text :disabled="loading" @click="emit('cancel')" />
      <Button type="submit" :label="submitLabel" icon="pi pi-check" :loading="loading" />
    </div>
  </form>
</template>
