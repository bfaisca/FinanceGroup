<script setup lang="ts">
import { reactive } from 'vue'
import Button from 'primevue/button'
import InputText from 'primevue/inputtext'
import Message from 'primevue/message'
import FormField from '@/components/FormField.vue'
import { useFormErrors } from '@/composables/useFormErrors'
import { normalizeEmail, validateEmail } from '@/utils/validation'

withDefaults(defineProps<{ loading?: boolean; errorMessage?: string | null; disabled?: boolean }>(), {
  loading: false,
  errorMessage: null,
  disabled: false,
})
const emit = defineEmits<{ submit: [email: string] }>()

const form = reactive({ email: '' })
const { errors, clear, setLocal } = useFormErrors(['email'] as const)

function onSubmit(): void {
  if (!setLocal({ email: validateEmail(form.email) })) return
  emit('submit', normalizeEmail(form.email))
}

/** Limpa o campo após um convite criado com sucesso. */
function reset(): void {
  form.email = ''
  clear()
}

defineExpose({ reset })
</script>

<template>
  <form class="invite-form" novalidate aria-label="Convidar por e-mail" @submit.prevent="onSubmit">
    <Message v-if="errorMessage" severity="error" role="alert" data-testid="invite-error">{{ errorMessage }}</Message>
    <div class="invite-form__row">
      <FormField
        id="invite-email"
        v-slot="{ id, describedBy, invalid }"
        label="E-mail do convidado"
        :error="errors.email"
        hint="O convidado precisa entrar (ou criar conta) com este mesmo e-mail para aceitar."
        class="invite-form__field"
      >
        <InputText
          :id="id"
          v-model="form.email"
          type="email"
          name="email"
          inputmode="email"
          autocomplete="off"
          placeholder="nome@exemplo.com"
          :aria-describedby="describedBy"
          :aria-invalid="invalid"
          :invalid="invalid"
          :disabled="disabled"
          fluid
          @input="clear('email')"
        />
      </FormField>
      <Button
        class="invite-form__submit"
        type="submit"
        label="Enviar convite"
        icon="pi pi-send"
        :loading="loading"
        :disabled="disabled"
      />
    </div>
  </form>
</template>

<style scoped>
.invite-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.invite-form__row {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  align-items: flex-start;
}

.invite-form__field {
  flex: 1 1 18rem;
  min-width: 0;
}

.invite-form__submit {
  margin-top: 1.625rem;
}

@media (max-width: 480px) {
  .invite-form__submit {
    margin-top: 0;
    width: 100%;
  }
}
</style>
